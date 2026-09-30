using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarkZither.KimaiDotNet.Models;

public sealed class ReportRecord {
 public int Id,UserId,ProjectId,ActivityId;
 public string Project,Activity,Comment;
 public DateTime Begin;
 public DateTime? End;
 public int? Seconds;
 public bool Running=>!End.HasValue;
}
public sealed class ReportUser {
 public int Id;
 public string Name;
 public bool? Enabled;
}
public sealed class ReportSnapshot {
 public List<ReportRecord> Records=new List<ReportRecord>();
 public List<ReportUser> Users=new List<ReportUser>();
 public string Notice,Timezone;
 public DateTime? From,Through;
 public DateTime Retrieved;
}
public sealed partial class KimaiService {
 public async Task<ReportSnapshot> ReadReportAsync(DateTime? from,DateTime? through,IProgress<string> progress,CancellationToken cancellation) {
  if(from.HasValue!=through.HasValue||from>through||through?.Date==DateTime.MaxValue.Date)throw new ArgumentException("開始日・終了日を確認してください。");
  var result=new ReportSnapshot {From=from?.Date,Through=through?.Date,Timezone=Me.Timezone,Notice="APIが閲覧を許可した範囲の集計です。全員・全実績を取得できたことを保証するものではありません。"};
  progress?.Report("ユーザー一覧を取得しています…");
  try {
   var users=await client.Api.Users.GetAsync(c=>{c.QueryParameters.Visible="3";c.QueryParameters.Full="false";},cancellation);
   result.Users=(users??new List<UserCollection>()).Where(u=>u.Id.HasValue).GroupBy(u=>u.Id.Value).Select(g=>g.First()).Select(u=>new ReportUser {Id=u.Id.Value,Name=(string.IsNullOrWhiteSpace(u.Alias)?u.Username:u.Alias+" / "+u.Username)+" [#"+u.Id+"]",Enabled=u.Enabled}).ToList();
  }catch(KimaiFailure ex) when(ex.Message.StartsWith("HTTP 403:")) {result.Notice+=" ユーザー一覧の権限がないため、本人と実績内のユーザーIDのみ表示します。";}
  if(!result.Users.Any(u=>u.Id==Me.Id))result.Users.Add(new ReportUser {Id=Me.Id.Value,Name=Me.Username+" [#"+Me.Id+"]"});
  var zone=TimeZoneInfo.FindSystemTimeZoneById(Me.Timezone??TimeZoneInfo.Local.Id);
  var records=new Dictionary<int,ReportRecord>();
  for(int page=1;page<=1000;page++) {
   cancellation.ThrowIfCancellationRequested();int current=page;
   progress?.Report("実績を取得しています… "+records.Count+"件 / "+page+"ページ");
   var batch=await client.Api.Timesheets.GetAsync(c=>{var q=c.QueryParameters;q.User="all";q.Full="false";q.Page=current.ToString();q.Size="500";q.OrderBy="id";q.Order="ASC";if(from.HasValue){q.Begin=LocalDate(from.Value.Date);q.End=LocalDate(through.Value.Date.AddDays(1).AddSeconds(-1));}},cancellation)??new List<TimesheetCollection>();
   int before=records.Count;
   foreach(var t in batch) {
    if(!t.Id.HasValue||!t.User.HasValue||!t.Project.HasValue||!t.Activity.HasValue||!t.Begin.HasValue)throw new KimaiFailure("集計用実績の必須項目が不足しています。取得を中止しました。");
    records[t.Id.Value]=new ReportRecord {Id=t.Id.Value,UserId=t.User.Value,ProjectId=t.Project.Value,ActivityId=t.Activity.Value,Project=ProjectName(t.Project.Value),Activity=ActivityName(t.Activity.Value),Comment=t.Description??"",Begin=TimeZoneInfo.ConvertTime(t.Begin.Value,zone).DateTime,End=t.End.HasValue?TimeZoneInfo.ConvertTime(t.End.Value,zone).DateTime:null,Seconds=t.End.HasValue&&t.Duration>=0?t.Duration:null};
   }
   if(guard.TotalPages.HasValue?page>=guard.TotalPages.Value:batch.Count<500)break;
   if(records.Count==before)throw new KimaiFailure("ページ取得が進みません。部分的な結果は集計しません。");
   if(page==1000)throw new KimaiFailure("50万件の取得上限です。期間を短くしてください。部分的な結果は集計しません。");
  }
  cancellation.ThrowIfCancellationRequested();result.Records=records.Values.ToList();
  foreach(int id in result.Records.Select(r=>r.UserId).Distinct().Except(result.Users.Select(u=>u.Id)))result.Users.Add(new ReportUser {Id=id,Name="ユーザー [#"+id+"]"});
  result.Retrieved=DateTime.Now;return result;
 }
}

