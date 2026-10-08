using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

public sealed class ProjectPeriodSpec {
 public int ProjectId {get;set;}
 public string ProjectName {get;set;}="";
 public string Header {get;set;}="第1期間";
 public DateTime? From {get;set;}
 public DateTime? Through {get;set;}
}
public sealed class PeriodReportConfig {
 public int FormatVersion {get;set;}=1;
 public string ServerUrl {get;set;}="";
 public List<ProjectPeriodSpec> Rows {get;set;}=new List<ProjectPeriodSpec>();
 public void Validate(){if(FormatVersion!=1||Rows==null||Rows.Count>500)throw new ArgumentException("期間設定は形式バージョン1、最大500行で指定してください。");foreach(var row in Rows){if(row==null||row.ProjectId<=0||string.IsNullOrWhiteSpace(row.Header)||row.Header.Length>80||row.From>row.Through||row.From==DateTime.MinValue||row.Through==DateTime.MinValue||row.Through?.Date==DateTime.MaxValue.Date)throw new ArgumentException("プロジェクト、ヘッダー名、開始日、終了日を確認してください。");}if(Rows.GroupBy(r=>(r.ProjectId,Header:r.Header.Trim())).Any(g=>g.Count()>1))throw new ArgumentException("同じプロジェクトに同じヘッダー名は複数指定できません。");}
 public static PeriodReportConfig Read(string json){var config=JsonSerializer.Deserialize<PeriodReportConfig>(json,SettingsLayers.Options)??throw new ArgumentException("期間設定がありません。");config.Validate();return config;}
}
public static class AdvancedReportTables {
 public static ReportSnapshot Exclude(ReportSnapshot data,IEnumerable<int> excluded)=>MemberReports.Filter(data,data.Users.Select(u=>u.Id).Except(excluded??Array.Empty<int>()).ToHashSet());
 public static DataTable Daily(ReportSnapshot data,DateTime from,DateTime through) {
  from=from.Date;through=through.Date;if(through<from||(through-from).Days>30)throw new ArgumentException("日別入力状況は最大31日です。月または週を指定してください。");
  var table=new DataTable("ユーザー入力状況（日別）");table.Columns.Add("アカウント名",typeof(string));for(var day=from;day<=through;day=day.AddDays(1))table.Columns.Add(day.ToString("yyyy-MM-dd"),typeof(decimal));table.Columns.Add("合計(h)",typeof(decimal));
  var seconds=data.Records.Where(r=>r.Seconds.HasValue&&r.Begin.Date>=from&&r.Begin.Date<=through).GroupBy(r=>(r.UserId,Day:r.Begin.Date)).ToDictionary(g=>g.Key,g=>g.Sum(r=>(long)r.Seconds.Value));
  foreach(var user in data.Users.OrderBy(u=>u.Name)){var row=table.NewRow();row[0]=user.Name;decimal total=0;for(var day=from;day<=through;day=day.AddDays(1)){decimal hours=seconds.GetValueOrDefault((user.Id,day))/3600m;row[day.ToString("yyyy-MM-dd")]=hours;total+=hours;}row["合計(h)"]=total;table.Rows.Add(row);}return table;
 }
 public static DataTable ProjectDetails(ReportSnapshot data,ISet<int> projects) {
  var table=new DataTable("プロジェクト実績明細");foreach(var name in new[]{"プロジェクト名","アカウント名","日付","開始時刻","終了時刻"})table.Columns.Add(name,typeof(string));table.Columns.Add("作業時間(h)",typeof(decimal));table.Columns.Add("作業時間(分)",typeof(decimal));table.Columns.Add("アクティビティ名",typeof(string));
  var users=data.Users.ToDictionary(u=>u.Id,u=>u.Name);
  foreach(var r in data.Records.Where(r=>projects==null||projects.Contains(r.ProjectId)).OrderBy(r=>r.Project).ThenBy(r=>r.Begin).ThenBy(r=>r.UserId))table.Rows.Add(r.Project,users.GetValueOrDefault(r.UserId,"ユーザー #"+r.UserId),r.Begin.ToString("yyyy-MM-dd"),r.Begin.ToString("HH:mm:ss"),r.End?.ToString(r.End.Value.Date==r.Begin.Date?"HH:mm:ss":"yyyy-MM-dd HH:mm:ss")??"",r.Seconds.HasValue?(object)(r.Seconds.Value/3600m):DBNull.Value,r.Seconds.HasValue?(object)(r.Seconds.Value/60m):DBNull.Value,r.Activity);
  return table;
 }
 public static DataTable Periods(ReportSnapshot data,PeriodReportConfig config,IEnumerable<(int ProjectId,int ActivityId,string Name)> activities=null,CancellationToken cancellation=default) {
  config.Validate();var headers=config.Rows.Select(r=>r.Header.Trim()).Distinct().ToList();var table=new DataTable("プロジェクト別・複数期間集計");table.Columns.Add("プロジェクト",typeof(string));table.Columns.Add("アクティビティ",typeof(string));foreach(var header in headers){var column=table.Columns.Add("期間"+table.Columns.Count,typeof(decimal));column.Caption=header+" (h)";}table.Columns.Add("期間列の合計(h)",typeof(decimal));
  var byProject=data.Records.ToLookup(r=>r.ProjectId);foreach(var project in config.Rows.GroupBy(r=>r.ProjectId)) {
   cancellation.ThrowIfCancellationRequested();var records=byProject[project.Key].ToList();var labels=(activities??Array.Empty<(int,int,string)>()).Where(a=>a.ProjectId==project.Key).Select(a=>(Id:a.ActivityId,Name:a.Name)).Concat(records.Select(r=>(Id:r.ActivityId,Name:r.Activity))).GroupBy(a=>a.Id).Select(g=>g.First()).OrderBy(a=>a.Name).ToList();
   var periods=project.ToDictionary(r=>r.Header.Trim());
   void Add(int? activity,string label){cancellation.ThrowIfCancellationRequested();var row=table.NewRow();row[0]=project.First().ProjectName;row[1]=label;decimal total=0;for(int i=0;i<headers.Count;i++){if(!periods.TryGetValue(headers[i],out var span)){row[i+2]=DBNull.Value;continue;}decimal hours=records.Where(r=>(!activity.HasValue||r.ActivityId==activity)&&(!span.From.HasValue||r.Begin.Date>=span.From.Value.Date)&&(!span.Through.HasValue||r.Begin.Date<=span.Through.Value.Date)&&r.Seconds.HasValue).Sum(r=>(long)r.Seconds.Value)/3600m;row[i+2]=hours;total+=hours;}row[table.Columns.Count-1]=total;table.Rows.Add(row);}
   foreach(var activity in labels)Add(activity.Id,activity.Name);Add(null,"【プロジェクト合計】");
  }
  return table;
 }
 public static DataTable PeriodSummary(DataTable details) {
  var table=new DataTable("プロジェクト別・期間合計");foreach(DataColumn column in details.Columns)if(column.ColumnName!="アクティビティ")table.Columns.Add(new DataColumn(column.ColumnName,column.DataType){Caption=column.Caption});
  foreach(DataRow row in details.Rows)if((string)row[1]=="【プロジェクト合計】")table.Rows.Add(table.Columns.Cast<DataColumn>().Select(c=>row[c.ColumnName]).ToArray());return table;
 }
 public static string Csv(DataTable table) {
  string Escape(string text){text??="";if(text.Length>0&&"=+-@\t\r".Contains(text[0]))text="'"+text;return "\""+text.Replace("\"","\"\"")+"\"";}
  var output=new StringBuilder();output.AppendLine(string.Join(",",table.Columns.Cast<DataColumn>().Select(c=>Escape(c.Caption))));foreach(DataRowView row in table.DefaultView)output.AppendLine(string.Join(",",table.Columns.Cast<DataColumn>().Select(c=>row[c.ColumnName] is decimal number?number.ToString("0.########",CultureInfo.InvariantCulture):Escape(Convert.ToString(row[c.ColumnName],CultureInfo.InvariantCulture)))));return output.ToString();
 }
}
