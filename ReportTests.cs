using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class ReportMock : HttpMessageHandler {
 public bool DenyUsers,FailSecond,DenyTimesheets,RepeatPage;
 public List<string> Queries=new List<string>();
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation) {
  cancellation.ThrowIfCancellationRequested();if(request.Method!=HttpMethod.Get)throw new Exception("Reporting must be read-only");
  string path=request.RequestUri.AbsolutePath,query=Uri.UnescapeDataString(request.RequestUri.Query);Queries.Add(query);
  HttpResponseMessage Reply(string json,HttpStatusCode status=HttpStatusCode.OK)=>new HttpResponseMessage(status){Content=new StringContent(json,Encoding.UTF8,"application/json")};
  if(path.EndsWith("/users/me"))return Task.FromResult(Reply("{\"id\":7,\"username\":\"self\",\"timezone\":\"Asia/Tokyo\"}"));
  if(path.EndsWith("/users"))return Task.FromResult(DenyUsers?Reply("{}",HttpStatusCode.Forbidden):Reply("[{\"id\":7,\"username\":\"self\",\"enabled\":true},{\"id\":8,\"username\":\"other\",\"enabled\":true},{\"id\":9,\"username\":\"zero\",\"enabled\":false}]"));
  if(!path.EndsWith("/timesheets")||!query.Contains("user=all")||!query.Contains("size=500")||!query.Contains("orderBy=id"))throw new Exception("Invalid report query");
  bool second=query.Contains("page=2");if(DenyTimesheets)return Task.FromResult(Reply("{}",HttpStatusCode.Forbidden));if(second&&FailSecond)return Task.FromResult(Reply("{}",HttpStatusCode.InternalServerError));
  string first="[{\"id\":1,\"user\":7,\"project\":11,\"activity\":21,\"begin\":\"2026-09-21T00:00:00+00:00\",\"end\":\"2026-09-21T01:00:00+00:00\",\"duration\":1800,\"break\":1800,\"description\":\"A\"}]";
  string last="[{\"id\":2,\"user\":8,\"project\":11,\"activity\":21,\"begin\":\"2026-09-22T10:00:00+09:00\",\"end\":null,\"duration\":900},{\"id\":3,\"user\":8,\"project\":12,\"activity\":22,\"begin\":\"2026-09-22T12:00:00+09:00\",\"end\":\"2026-09-22T13:00:00+09:00\"}]";
  var response=Reply(second&&!RepeatPage?last:first);response.Headers.Add("X-Total-Pages",RepeatPage?"3":"2");return Task.FromResult(response);
 }
}
public static class ReportTests {
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 public static async Task Run() {
  var mock=new ReportMock();using var api=new KimaiService("https://example.test","test-token","",false,mock);await api.InitializeAsync(false);
  var data=await api.ReadReportAsync(new DateTime(2026,9,21),new DateTime(2026,9,22),null,CancellationToken.None);
  Check(data.Records.Count==3&&data.Records[0].Begin.Hour==9&&data.Records[0].Seconds==1800,"Report timezone/duration/pagination");
  Check(mock.Queries.Last().Contains("begin=2026-09-21T00:00:00")&&mock.Queries.Last().Contains("end=2026-09-22T23:59:59"),"Report inclusive range");
  var tables=ReportTables.Build(data);var users=tables[0];
  var filterTable=new DataTable();filterTable.Columns.Add("text",typeof(string));filterTable.Rows.Add("a['%*]b");filterTable.Rows.Add("other");filterTable.DefaultView.RowFilter=ReportTables.FilterExpression(filterTable,"['%*]");Check(filterTable.DefaultView.Count==1,"Table search did not escape literal characters");
  Check(users.Rows.Count==3&&(int)users.Rows[2]["実績件数"]==0&&users.Rows[2]["取得状態"].ToString().Contains("断定不可"),"Zero-user reporting");
  Check(tables.Single(t=>t.TableName=="プロジェクト合計").Rows.Cast<DataRow>().Sum(r=>(decimal)r["完了時間(h)"])==0.5m,"Report used wall duration or running duration");
  Check(tables.Single(t=>t.TableName=="実績明細").Rows.Cast<DataRow>().Count(r=>r.IsNull("時間(h)"))==2,"Unknown duration presented as zero");
  await api.ReadReportAsync(null,null,null,CancellationToken.None);Check(!mock.Queries.Last().Contains("begin=")&&!mock.Queries.Last().Contains("end="),"All-time query limited");
  mock.DenyUsers=true;data=await api.ReadReportAsync(null,null,null,CancellationToken.None);Check(data.Users.Count==2&&data.Notice.Contains("権限がない"),"Roster permission fallback");
  mock.FailSecond=true;try{await api.ReadReportAsync(null,null,null,CancellationToken.None);throw new Exception("Partial result accepted");}catch(KimaiFailure){}mock.FailSecond=false;
  mock.DenyTimesheets=true;try{await api.ReadReportAsync(null,null,null,CancellationToken.None);throw new Exception("Forbidden report accepted");}catch(KimaiFailure){}mock.DenyTimesheets=false;
  mock.RepeatPage=true;try{await api.ReadReportAsync(null,null,null,CancellationToken.None);throw new Exception("Repeated pages accepted");}catch(KimaiFailure){}
  using var cancel=new CancellationTokenSource();cancel.Cancel();try{await api.ReadReportAsync(null,null,null,cancel.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
  Console.WriteLine("PASS: report permissions, paging, all-time/range queries, server durations, zero users, missing durations, partial failure and cancellation.");
 }
}
