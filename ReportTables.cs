using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;

public static class ReportTables {
 public static string FilterExpression(DataTable table,string text) {
  string term=string.Concat(text.Trim().Select(c=>c switch {'\''=>"''",'['=>"[[]",']'=>"[]]",'%'=>"[%]",'*'=>"[*]",_=>c.ToString()}));
  return term.Length==0?"":string.Join(" OR ",table.Columns.Cast<DataColumn>().Select(c=>"Convert(["+c.ColumnName+"], 'System.String') LIKE '%"+term+"%'"));
 }
 static DataTable Table(string name,params (string Name,Type Type)[] columns){var t=new DataTable(name);foreach(var c in columns)t.Columns.Add(c.Name,c.Type);return t;}
 static decimal Hours(IEnumerable<ReportRecord> rows)=>Math.Round(rows.Sum(r=>(long)(r.Seconds??0))/3600m,4);
 static object Date(DateTime? value)=>value.HasValue?(object)value.Value:DBNull.Value;
 public static List<DataTable> Build(ReportSnapshot data,CancellationToken cancellation=default) {
  cancellation.ThrowIfCancellationRequested();
  var names=data.Users.ToDictionary(u=>u.Id,u=>u.Name);string User(int id)=>names.TryGetValue(id,out var name)?name:"ユーザー [#"+id+"]";
  var users=Table("ユーザー入力状況",("ユーザー",typeof(string)),("有効状態",typeof(string)),("取得状態",typeof(string)),("実績件数",typeof(int)),("入力日数",typeof(int)),("完了時間(h)",typeof(decimal)),("計測中",typeof(int)),("時間不明",typeof(int)),("最初の開始",typeof(DateTime)),("最後の開始",typeof(DateTime)));
  var byUser=data.Records.ToLookup(r=>r.UserId);
  foreach(var u in data.Users.OrderBy(u=>u.Name)){var rows=byUser[u.Id].ToList();users.Rows.Add(u.Name,u.Enabled.HasValue?(u.Enabled.Value?"有効":"無効"):"不明",rows.Count==0?"取得0件（未入力とは断定不可）":"実績あり",rows.Count,rows.Select(r=>r.Begin.Date).Distinct().Count(),Hours(rows),rows.Count(r=>r.Running),rows.Count(r=>!r.Running&&!r.Seconds.HasValue),Date(rows.Count==0?null:rows.Min(r=>r.Begin)),Date(rows.Count==0?null:rows.Max(r=>r.Begin)));}
  DataTable Summary(string title,IEnumerable<IGrouping<string,ReportRecord>> groups,string label) {
   var t=Table(title,(label,typeof(string)),("実績件数",typeof(int)),("ユーザー数",typeof(int)),("完了時間(h)",typeof(decimal)),("計測中",typeof(int)),("時間不明",typeof(int)));
   foreach(var g in groups.OrderByDescending(g=>Hours(g)))t.Rows.Add(g.Key,g.Count(),g.Select(r=>r.UserId).Distinct().Count(),Hours(g),g.Count(r=>r.Running),g.Count(r=>!r.Running&&!r.Seconds.HasValue));return t;
  }
  var projects=Summary("プロジェクト合計",data.Records.GroupBy(r=>r.Project),"プロジェクト");
  var projectUsers=Table("プロジェクト×ユーザー",("プロジェクト",typeof(string)),("ユーザー",typeof(string)),("実績件数",typeof(int)),("完了時間(h)",typeof(decimal)),("計測中",typeof(int)));
  foreach(var g in data.Records.GroupBy(r=>(r.ProjectId,r.UserId)).OrderBy(g=>g.First().Project).ThenBy(g=>User(g.Key.UserId)))projectUsers.Rows.Add(g.First().Project,User(g.Key.UserId),g.Count(),Hours(g),g.Count(r=>r.Running));
  var details=Table("実績明細",("実績ID",typeof(int)),("プロジェクト",typeof(string)),("ユーザー",typeof(string)),("アクティビティ",typeof(string)),("開始",typeof(DateTime)),("終了",typeof(DateTime)),("時間(h)",typeof(decimal)),("秒数",typeof(int)),("状態",typeof(string)),("コメント",typeof(string)));
  foreach(var r in data.Records.OrderBy(r=>r.Project).ThenBy(r=>r.Begin)){cancellation.ThrowIfCancellationRequested();details.Rows.Add(r.Id,r.Project,User(r.UserId),r.Activity,r.Begin,Date(r.End),r.Seconds.HasValue?(object)(r.Seconds.Value/3600m):DBNull.Value,r.Seconds.HasValue?(object)r.Seconds.Value:DBNull.Value,r.Running?"計測中":r.Seconds.HasValue?"完了":"時間不明",r.Comment);}
  var projectActivities=Table("プロジェクト×アクティビティ",("プロジェクト",typeof(string)),("アクティビティ",typeof(string)),("実績件数",typeof(int)),("ユーザー数",typeof(int)),("完了時間(h)",typeof(decimal)),("計測中",typeof(int)));
  foreach(var g in data.Records.GroupBy(r=>(r.ProjectId,r.ActivityId)).OrderBy(g=>g.First().Project))projectActivities.Rows.Add(g.First().Project,g.First().Activity,g.Count(),g.Select(r=>r.UserId).Distinct().Count(),Hours(g),g.Count(r=>r.Running));
  var activities=Summary("アクティビティ別",data.Records.GroupBy(r=>r.Activity),"アクティビティ");
  var months=Summary("月別",data.Records.GroupBy(r=>r.Begin.ToString("yyyy-MM")),"月（開始日基準）");
  var comments=Table("プロジェクト×コメント",("プロジェクト",typeof(string)),("コメント",typeof(string)),("実績件数",typeof(int)),("ユーザー数",typeof(int)),("完了時間(h)",typeof(decimal)));
  foreach(var g in data.Records.GroupBy(r=>(r.ProjectId,Comment:(r.Comment??"").Replace("\r\n","\n").Trim())).OrderBy(g=>g.First().Project))comments.Rows.Add(g.First().Project,g.Key.Comment,g.Count(),g.Select(r=>r.UserId).Distinct().Count(),Hours(g));
  cancellation.ThrowIfCancellationRequested();return new List<DataTable>{users,projects,projectUsers,projectActivities,details,activities,months,comments};
 }
}
