using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

// Offline contract tests exercise the actual Kiota client/serialization over a fake transport.
public sealed class MockKimai : HttpMessageHandler {
 public string Record="{\"id\":41,\"project\":11,\"activity\":22,\"user\":7,\"begin\":\"2026-09-21T09:05:00+09:00\",\"end\":\"2026-09-21T09:10:00+09:00\",\"description\":\"original\",\"billable\":true,\"exported\":false,\"break\":0,\"tags\":[\"keep\"],\"hourlyRate\":125}";
 public List<string> Methods=new List<string>();
 public List<string> Queries=new List<string>();
 public string LastBody;
 public bool Legacy,Paginated,HeaderPagination,Conflict,FailWrite,TimeoutWrite;
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {
  if(!request.RequestUri.AbsolutePath.StartsWith("/kimai/api/"))throw new Exception("Subdirectory lost");
  if(Legacy){if(request.Headers.Authorization!=null||!request.Headers.Contains("X-AUTH-USER")||!request.Headers.Contains("X-AUTH-TOKEN"))throw new Exception("Legacy authentication failed");}
  else if(request.Headers.Authorization?.Scheme!="Bearer"||request.Headers.Contains("X-AUTH-TOKEN"))throw new Exception("Bearer authentication failed");
  string path=request.RequestUri.AbsolutePath;Methods.Add(request.Method.Method+" "+path);Queries.Add(request.RequestUri.Query);
  if(request.Method!=HttpMethod.Get) {
   if(TimeoutWrite)throw new HttpRequestException("Simulated connection interruption");
   if(FailWrite)return Reply("{}",HttpStatusCode.InternalServerError);
   if(request.Method==HttpMethod.Delete)return new HttpResponseMessage(HttpStatusCode.NoContent);
   LastBody=await request.Content.ReadAsStringAsync(token);var body=JsonNode.Parse(LastBody);var rec=JsonNode.Parse(Record);
   foreach(var key in new[]{"project","activity","description","billable","exported"})rec[key]=body[key]?.DeepClone();
   rec["begin"]=body["begin"].GetValue<string>()+"+09:00";rec["end"]=body["end"].GetValue<string>()+"+09:00";
   Record=rec.ToJsonString();return Reply(Record,request.Method==HttpMethod.Post?HttpStatusCode.Created:HttpStatusCode.OK);
  }
  if(path.EndsWith("/users/me"))return Reply("{\"id\":7,\"username\":\"test\",\"timezone\":\"Asia/Tokyo\"}");
  if(path.EndsWith("/projects"))return Reply("[{\"id\":11,\"name\":\"同名\",\"visible\":true,\"globalActivities\":true},{\"id\":12,\"name\":\"同名\",\"visible\":true,\"globalActivities\":false}]");
  if(path.EndsWith("/activities"))return Reply("[{\"id\":22,\"project\":11,\"name\":\"設計\",\"visible\":true},{\"id\":23,\"project\":null,\"name\":\"共通\",\"visible\":true},{\"id\":24,\"project\":12,\"name\":\"実装\",\"visible\":true}]");
  if(path.EndsWith("/timesheets")) {
   if(HeaderPagination){bool lastPage=request.RequestUri.Query.Contains("page=2");var row=JsonNode.Parse(Record);row["id"]=lastPage?202:201;var response=Reply("["+row.ToJsonString()+"]");response.Headers.Add("X-Total-Pages","2");return response;}
   if(!Paginated)return Reply("["+Record+"]");
   bool second=request.RequestUri.Query.Contains("page=2");var rows=new JsonArray();for(int i=0;i<(second?1:100);i++){var row=JsonNode.Parse(Record);row["id"]=second?200:100+i;rows.Add(row);}return Reply(rows.ToJsonString());
  }
  if(path.EndsWith("/timesheets/41")){if(Conflict){var row=JsonNode.Parse(Record);row["description"]="changed elsewhere";return Reply(row.ToJsonString());}return Reply(Record);}
  return Reply("{}",HttpStatusCode.NotFound);
 }
 static HttpResponseMessage Reply(string json,HttpStatusCode status=HttpStatusCode.OK){return new HttpResponseMessage(status){Content=new StringContent(json,Encoding.UTF8,"application/json")};}
}
public static class KimaiServiceTests {
 static void Check(bool success,string message){if(!success)throw new Exception(message);}
 public static async Task Run() {
  Check(KimaiService.NormalizeUrl("https://kimai.test/kimai/api/")=="https://kimai.test/kimai","Normalize URL");
  Check(KimaiService.NormalizeUrl("http://127.0.0.1:8001/",true)=="http://127.0.0.1:8001","Explicit HTTP option");
  foreach(var url in new[]{"http://kimai.test","https://user:pass@kimai.test","https://kimai.test/?token=x"}){bool rejected=false;try{KimaiService.NormalizeUrl(url);}catch(ArgumentException){rejected=true;}Check(rejected,"Unsafe URL accepted");}
  var mock=new MockKimai();using(var api=new KimaiService("https://kimai.test/kimai","test-only-token","",false,mock)) {
   await api.InitializeAsync();Check(api.Projects.Count==2,"Project retrieval");Check(api.ProjectName(11)!=api.ProjectName(12),"Duplicate names merged");Check(api.ForProject(11).Count()==2&&api.ForProject(12).Count()==1,"Project activity filtering");
   var week=new DateTime(2026,9,21);var entries=await api.ReadWeekAsync(week);var original=entries.Single();Check(original.Start.Hour==9&&original.Start.Minute==5&&original.Minutes==5,"Timezone conversion changed wall time");
   Check(mock.Queries.Last().Contains("user=7")&&mock.Queries.Last().Contains("begin="),"Own-week filtering missing");
   var edited=original.Copy();edited.Start=edited.Start.AddMinutes(5);edited.Note="updated";
   var saved=await api.WriteAsync(edited,original);using(var json=JsonDocument.Parse(mock.LastBody)){var r=json.RootElement;Check(r.GetProperty("begin").GetString()=="2026-09-21T09:10:00","Write contains timezone offset");Check(r.GetProperty("end").GetString()=="2026-09-21T09:15:00","Five minute duration changed");Check(r.GetProperty("tags").GetString()=="keep"&&r.GetProperty("hourlyRate").GetDouble()==125,"Metadata lost");Check(r.GetProperty("billable").GetBoolean()&&!r.GetProperty("exported").GetBoolean(),"Booleans lost");}
   Check(saved.RemoteId==41&&saved.Note=="updated","PATCH response mapping");
   mock.Conflict=true;int mutations=mock.Methods.Count(x=>x.StartsWith("PATCH"));bool rejected=false;try{await api.WriteAsync(saved.Copy(),saved);}catch(KimaiFailure){rejected=true;}Check(rejected&&mutations==mock.Methods.Count(x=>x.StartsWith("PATCH")),"Conflict was overwritten");mock.Conflict=false;
   await api.DeleteAsync(saved);Check(mock.Methods.Last().StartsWith("DELETE"),"Delete request missing");
   var fresh=saved.Copy();fresh.RemoteId=0;fresh.Fingerprint=null;fresh.Start=week.AddYears(1).AddHours(23).AddMinutes(55);fresh.Minutes=5;
   await api.WriteAsync(fresh,null);Check(mock.Methods.Last().StartsWith("POST"),"Create request missing");
   using(var json=JsonDocument.Parse(mock.LastBody)){Check(json.RootElement.GetProperty("end").GetString()=="2027-09-22T00:00:00","Future midnight boundary");}
   mock.Paginated=true;var many=await api.ReadWeekAsync(week);Check(many.Count==101,"Pagination truncated");mock.Paginated=false;
   mock.HeaderPagination=true;many=await api.ReadWeekAsync(week);Check(many.Count==2,"Pagination headers ignored");mock.HeaderPagination=false;
   var special=new MarkZither.KimaiDotNet.Models.TimesheetEntity {Id=90,Project=11,Activity=22,User=7,Begin=new DateTimeOffset(2026,9,21,9,0,0,TimeSpan.FromHours(9)),End=null};
   Check(api.MapEntity(special).ReadOnlyReason!=null,"Running entry must be read-only");special.End=special.Begin.Value.AddMinutes(5);special.Exported=true;Check(api.MapEntity(special).ReadOnlyReason!=null,"Exported entry must be read-only");
   special.Exported=false;special.End=special.Begin.Value.AddDays(1);special.Begin=special.Begin.Value.AddSeconds(3);Check(api.MapEntity(special).ReadOnlyReason!=null,"Non-grid entry must not be rounded and overwritten");
   mock.FailWrite=true;int calls=mock.Methods.Count;bool uncertain=false;try{await api.WriteAsync(fresh,null);}catch(KimaiFailure ex){uncertain=ex.Uncertain;}Check(uncertain&&mock.Methods.Count==calls+1,"Server failure retried automatically");mock.FailWrite=false;
   mock.TimeoutWrite=true;calls=mock.Methods.Count;uncertain=false;try{await api.WriteAsync(fresh,null);}catch(KimaiFailure ex){uncertain=ex.Uncertain;}Check(uncertain&&mock.Methods.Count==calls+1,"Timeout retried automatically");
  }
  using(var legacy=new KimaiService("https://kimai.test/kimai","test-only-token","test",true,new MockKimai {Legacy=true})){await legacy.InitializeAsync();}
 }
}

