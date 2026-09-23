using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using MarkZither.KimaiDotNet;
using MarkZither.KimaiDotNet.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Http.HttpClientLibrary;

// Kimai expects user-local wall times without a timezone offset when writing.
// Override only serialization: keep the SDK's request builders and response models.
public sealed class LocalTimesheetForm : TimesheetEditForm {
 public DateTime LocalBegin, LocalEnd;
 public override void Serialize(ISerializationWriter writer) {
  writer.WriteIntValue("project",Project);writer.WriteIntValue("activity",Activity);
  writer.WriteStringValue("begin",KimaiService.LocalDate(LocalBegin));
  writer.WriteStringValue("end",KimaiService.LocalDate(LocalEnd));
  writer.WriteStringValue("description",Description ?? "");
  writer.WriteBoolValue("billable",Billable);writer.WriteBoolValue("exported",Exported);
  if(Tags!=null)writer.WriteStringValue("tags",Tags);
  if(FixedRate.HasValue)writer.WriteDoubleValue("fixedRate",FixedRate);
  if(HourlyRate.HasValue)writer.WriteDoubleValue("hourlyRate",HourlyRate);
 }
}
public sealed class KimaiFailure : Exception {
 public bool Uncertain {get; private set;}
 public KimaiFailure(string message,bool uncertain=false) : base(message) {Uncertain=uncertain;}
}
// Plain HttpClient: no automatic retries of POST/PATCH/DELETE, no credential redirects.
public sealed class KimaiHttpGuard : DelegatingHandler {
 public int? TotalPages {get;private set;}
 public KimaiHttpGuard(HttpMessageHandler inner):base(inner) {}
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
  var response=await base.SendAsync(request,ct).ConfigureAwait(false);
  TotalPages=null;
  if(response.Headers.TryGetValues("X-Total-Pages",out var values)&&int.TryParse(values.FirstOrDefault(),out int pages))TotalPages=pages;
  if(response.IsSuccessStatusCode)return response;
  int code=(int)response.StatusCode;response.Dispose();
  string detail=code==401?"認証に失敗しました。トークンと認証方式を確認してください。":
   code==403?"この操作の権限がありません。":code==404?"APIまたは実績が見つかりません。URLを確認し、再読込してください。":
   code==400||code==422?"Kimaiが入力を受け付けませんでした。未来日時・重複・期間・必須項目のサーバー設定を確認してください。":
   code==429?"リクエスト数の制限に達しました。時間を置いて再読込してください。":
   code>=300&&code<400?"リダイレクト先へ認証情報は送信していません。最終的なKimai URLを入力してください。":"Kimaiとの通信に失敗しました。";
  throw new KimaiFailure("HTTP "+code+": "+detail,code>=500 && request.Method!=HttpMethod.Get);
 }
}
public sealed class KimaiService : IDisposable {
 readonly HttpClient http;
 readonly KimaiHttpGuard guard;
 readonly HttpClientRequestAdapter adapter;
 readonly KimaiClient client;
 internal KimaiClient TestClient {get{return client;}}
 public readonly string BaseUrl;
 public UserEntity Me {get;private set;}
 public List<ProjectCollection> Projects {get;private set;}=new List<ProjectCollection>();
 public List<ActivityCollection> Activities {get;private set;}=new List<ActivityCollection>();
 public KimaiService(string url,string token,string username,bool legacy,HttpMessageHandler handler=null,bool allowHttp=false) {
  BaseUrl=NormalizeUrl(url,allowHttp);
  guard=new KimaiHttpGuard(handler??new HttpClientHandler {AllowAutoRedirect=false,UseCookies=false});
  http=new HttpClient(guard) {Timeout=TimeSpan.FromSeconds(30)};
  if(string.IsNullOrWhiteSpace(token)||token.IndexOfAny(new[]{'\r','\n'})>=0)throw new ArgumentException("APIトークンを入力してください。");
  if(legacy){if(string.IsNullOrWhiteSpace(username))throw new ArgumentException("旧方式ではユーザー名が必要です。");http.DefaultRequestHeaders.Add("X-AUTH-USER",username);http.DefaultRequestHeaders.Add("X-AUTH-TOKEN",token);}
  else http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
  adapter=new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(),httpClient:http) {BaseUrl=BaseUrl};client=new KimaiClient(adapter);
 }
 public static string NormalizeUrl(string value,bool allowHttp=false) {
  Uri uri;if(!Uri.TryCreate(value.Trim(),UriKind.Absolute,out uri)||(uri.Scheme!="https"&&!(allowHttp&&uri.Scheme=="http"))||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment))throw new ArgumentException("KimaiのURLを確認してください。HTTPは「この接続先でHTTPを許可」を有効にした場合のみ利用できます。認証情報・クエリはURLに含めません。");
  string result=uri.AbsoluteUri.TrimEnd('/');if(result.EndsWith("/api",StringComparison.OrdinalIgnoreCase))result=result.Substring(0,result.Length-4);return result;
 }
 public static string LocalDate(DateTime value) {return value.ToString("yyyy-MM-dd'T'HH:mm:ss",CultureInfo.InvariantCulture);}
 public async Task InitializeAsync(bool catalog=true) {
  Me=await client.Api.Users.Me.GetAsync();if(Me?.Id==null)throw new KimaiFailure("ユーザー情報を読み取れませんでした。");
  if(catalog)await ReloadCatalogAsync();
 }
 public async Task ReloadCatalogAsync() {
  // On non-paginated older endpoints page/size are ignored: deduplication stops the loop.
  var projects=new Dictionary<int,ProjectCollection>();var activities=new Dictionary<int,ActivityCollection>();
  for(int page=1;page<=1000;page++) {
   var batch=await client.Api.Projects.WithUrl(BaseUrl+"/api/projects?visible=3&ignoreDates=1&size=100&page="+page).GetAsync()??new List<ProjectCollection>();
   int before=projects.Count;foreach(var p in batch)if(p.Id.HasValue)projects[p.Id.Value]=p;
   if(before==projects.Count){if(guard.TotalPages>page)throw new KimaiFailure("プロジェクトのページ取得が進みません。");break;}
   if(guard.TotalPages.HasValue?page>=guard.TotalPages.Value:batch.Count<100)break;if(page==1000)throw new KimaiFailure("プロジェクトの取得上限に達しました。");
  }
  for(int page=1;page<=1000;page++) {
   var batch=await client.Api.Activities.WithUrl(BaseUrl+"/api/activities?visible=3&size=100&page="+page).GetAsync()??new List<ActivityCollection>();
   int before=activities.Count;foreach(var a in batch)if(a.Id.HasValue)activities[a.Id.Value]=a;
   if(before==activities.Count){if(guard.TotalPages>page)throw new KimaiFailure("アクティビティのページ取得が進みません。");break;}
   if(guard.TotalPages.HasValue?page>=guard.TotalPages.Value:batch.Count<100)break;if(page==1000)throw new KimaiFailure("アクティビティの取得上限に達しました。");
  }
  Projects=projects.Values.ToList();Activities=activities.Values.ToList();
 }
 public void RestoreCatalog(CatalogSnapshot snapshot) {
  Projects=snapshot.Projects.Select(p=>new ProjectCollection {Id=p.Id,Name=p.Name,Visible=p.Visible,Billable=p.Billable,GlobalActivities=p.GlobalActivities}).ToList();
  Activities=snapshot.Activities.Select(a=>new ActivityCollection {Id=a.Id,Project=a.Project,Name=a.Name,Visible=a.Visible,Billable=a.Billable}).ToList();
 }
 public async Task<List<CustomerOption>> ReadCustomersAsync() {
  var customers=new Dictionary<int,CustomerOption>();
  for(int page=1;page<=1000;page++) {
   var batch=await client.Api.Customers.WithUrl(BaseUrl+"/api/customers?visible=1&size=100&page="+page).GetAsync()??new List<CustomerCollection>();
   int before=customers.Count;foreach(var c in batch)if(c.Id.HasValue&&c.Visible!=false)customers[c.Id.Value]=new CustomerOption {Id=c.Id.Value,Name=c.Name};
   if(guard.TotalPages.HasValue?page>=guard.TotalPages.Value:batch.Count<100)break;
   if(before==customers.Count){if(guard.TotalPages>page)throw new KimaiFailure("顧客のページ取得が進みません。");break;}
   if(page==1000)throw new KimaiFailure("顧客一覧の取得上限に達しました。");
  }
  return customers.Values.OrderBy(c=>c.Name).ToList();
 }
 public async Task<ProjectEntity> CreateGlobalProjectAsync(int customer,string name,string comment,bool billable) {
  var result=await client.Api.Projects.PostAsync(new ProjectEditForm {Customer=customer,Name=name,Comment=comment,Visible=true,Billable=billable,GlobalActivities=true});
  if(result?.Id==null)throw new KimaiFailure("プロジェクトの作成結果が不明です。再読込してください。",true);
  return result;
 }
 public string ProjectName(int id) {return (Projects.FirstOrDefault(p=>p.Id==id)?.Name??"プロジェクト")+" [#"+id+"]";}
 public string ActivityName(int id) {return (Activities.FirstOrDefault(a=>a.Id==id)?.Name??"アクティビティ")+" [#"+id+"]";}
 public IEnumerable<ActivityCollection> ForProject(int id) {
  var p=Projects.FirstOrDefault(x=>x.Id==id);
  return Activities.Where(a=>a.Visible!=false && (a.Project==id || ((!a.Project.HasValue||a.Project==0)&&p?.GlobalActivities!=false)));
 }
 public async Task<List<Entry>> ReadWeekAsync(DateTime week) {
  var entries=new Dictionary<int,Entry>();
  for(int page=1;page<=1000;page++) {
   int current=page;
   var batch=await client.Api.Timesheets.GetAsync(c=>{c.QueryParameters.User=Me.Id.Value.ToString(CultureInfo.InvariantCulture);c.QueryParameters.Begin=LocalDate(week);c.QueryParameters.End=LocalDate(week.AddDays(7).AddSeconds(-1));c.QueryParameters.Page=current.ToString();c.QueryParameters.Size="100";c.QueryParameters.OrderBy="id";c.QueryParameters.Order="ASC";c.QueryParameters.Full="false";})??new List<TimesheetCollection>();
   int before=entries.Count;
   foreach(var t in batch) {if(t.User!=Me.Id)throw new KimaiFailure("ユーザーの異なる実績が返されました。読み込みを中止しました。");if(t.Id.HasValue)entries[t.Id.Value]=Map(t.Id,t.Project,t.Activity,t.Begin,t.End,t.Description,t.Billable,t.Exported,t.Break,t.Tags);}
   if(guard.TotalPages.HasValue?page>=guard.TotalPages.Value:batch.Count<100)break;if(entries.Count==before)throw new KimaiFailure("実績のページ取得が進みませんでした。");if(page==1000)throw new KimaiFailure("実績の取得上限に達しました。");
  }
  return entries.Values.ToList();
 }
 Entry Map(int? id,int? project,int? activity,DateTimeOffset? begin,DateTimeOffset? end,string note,bool? billable,bool? exported,int? pause,List<string> tags) {
  if(!id.HasValue||!project.HasValue||!activity.HasValue||!begin.HasValue)throw new KimaiFailure("実績の必須項目が不足しています。");
  DateTime start=begin.Value.DateTime;DateTime finish=end?.DateTime??start.AddMinutes(5);
  double duration=(finish-start).TotalMinutes;
  string reason=exported==true?"エクスポート済み":!end.HasValue?"計測中（停止はKimaiで操作）":pause>0?"休憩を含む実績":duration<=0?"期間が不正":start.Date!=finish.AddTicks(-1).Date?"日をまたぐ実績":duration%5!=0||start.TimeOfDay.TotalMinutes%5!=0?"5分単位以外の実績":null;
  return new Entry {RemoteId=id.Value,ProjectId=project.Value,ActivityId=activity.Value,Project=ProjectName(project.Value),Activity=ActivityName(activity.Value),Start=start,Minutes=Math.Max(1,(int)Math.Ceiling(duration)),Note=note??"",Billable=billable??false,ReadOnlyReason=reason,Fingerprint=Stamp(project,activity,begin,end,note,billable,exported,pause,tags)};
 }
 static string Stamp(int? p,int? a,DateTimeOffset? b,DateTimeOffset? e,string n,bool? bill,bool? exp,int? pause,List<string> tags) {
  return JsonSerializer.Serialize(new {p,a,b,e,n=n??"",bill=bill??false,exp=exp??false,pause=pause??0,tags=tags??new List<string>()});
 }
 public Entry MapEntity(TimesheetEntity t) {return Map(t.Id,t.Project,t.Activity,t.Begin,t.End,t.Description,t.Billable,t.Exported,t.Break,t.Tags);}
 public async Task<Entry> FindEntryAsync(int id) {
  try {
   var entity=await client.Api.Timesheets[id.ToString(CultureInfo.InvariantCulture)].GetAsync();
   if(entity?.Id!=id||entity.User!=Me.Id)throw new KimaiFailure("実績の所有者を確認できません。");
   return MapEntity(entity);
  } catch(KimaiFailure ex) when(ex.Message.StartsWith("HTTP 404:")) {return null;}
 }
 async Task<TimesheetEntity> CheckCurrentAsync(Entry old) {
  var t=await client.Api.Timesheets[old.RemoteId.ToString()].GetAsync();
  if(t==null||t.User!=Me.Id||t.Id!=old.RemoteId)throw new KimaiFailure("実績を確認できません。再読込してください。");
  var current=MapEntity(t);
  if(current.Fingerprint!=old.Fingerprint)throw new KimaiFailure("Kimai側で実績が変更されています。再読込してから編集してください。");
  if(current.ReadOnlyReason!=null)throw new KimaiFailure("この実績は読み取り専用です: "+current.ReadOnlyReason);
  return t;
 }
 public async Task<Entry> WriteAsync(Entry edited,Entry old) {
  var p=Projects.FirstOrDefault(x=>x.Id==edited.ProjectId);
  if(p==null||p.Visible==false||!ForProject(edited.ProjectId).Any(a=>a.Id==edited.ActivityId))throw new KimaiFailure("選択したプロジェクト・アクティビティは現在利用できません。再読込してください。");
  var prior=old?.RemoteId>0?await CheckCurrentAsync(old):null;
  var form=new LocalTimesheetForm {Project=edited.ProjectId,Activity=edited.ActivityId,LocalBegin=edited.Start,LocalEnd=edited.Start.AddMinutes(edited.Minutes),Description=edited.Note,Billable=edited.Billable,Exported=false,Tags=prior==null?"":string.Join(",",prior.Tags??new List<string>()),FixedRate=prior?.FixedRate,HourlyRate=prior?.HourlyRate};
  try {
   var response=prior==null?await client.Api.Timesheets.PostAsync(form):await client.Api.Timesheets[edited.RemoteId.ToString()].PatchAsync(form);
   if(response==null||response.Id==null||response.User!=Me.Id)throw new KimaiFailure("保存結果を確認できません。再読込してください。",true);
   return MapEntity(response);
  } catch(KimaiFailure){throw;}catch {throw new KimaiFailure("保存結果が不明です。重複登録を避けるため、自動再送はしません。「再読込」でKimaiの実績を確認してください。",true);}
 }
 public async Task DeleteAsync(Entry old) {
  await CheckCurrentAsync(old);
  try {await client.Api.Timesheets[old.RemoteId.ToString()].DeleteAsync();}
  catch(KimaiFailure){throw;}catch {throw new KimaiFailure("削除結果が不明です。「再読込」でKimaiの実績を確認してください。",true);}
 }
 public void Dispose() {adapter.Dispose();http.Dispose();}
}
