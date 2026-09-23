using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
public partial class Blocks {
 string[] ActivitiesFor(string project) {
  if(service==null)return Array.Empty<string>();
  var p=service.Projects.FirstOrDefault(x=>service.ProjectName(x.Id.Value)==project);
  return p==null?Array.Empty<string>():service.ForProject(p.Id.Value).Select(a=>service.ActivityName(a.Id.Value)).ToArray();
 }
 bool AssignIds(Entry en) {
  if(service==null)return false;
  var p=service.Projects.FirstOrDefault(x=>service.ProjectName(x.Id.Value)==en.Project);
  var a=p==null?null:service.ForProject(p.Id.Value).FirstOrDefault(x=>service.ActivityName(x.Id.Value)==en.Activity);
  if(p==null||a==null){MessageBox.Show(this,"プロジェクトとアクティビティを選択してください。");return false;}
  en.ProjectId=p.Id.Value;en.ActivityId=a.Id.Value;
  if(en.RemoteId==0)en.Billable=(p.Billable??true)&&(a.Billable??true);
  return true;
 }
 string ColorFor(string project) {return Colors[Math.Max(0,Array.IndexOf(Projects,project))%Colors.Length];}
 List<Entry> VisibleEntries() {
  var result=new List<Entry>();
  foreach(var en in state.Entries) {
   DateTime end=en.Start.AddMinutes(en.Minutes);
   if(end<=week||en.Start>=week.AddDays(7))continue;
   if(en.Start.Date==end.AddTicks(-1).Date){result.Add(en);continue;}
   for(var day=en.Start.Date<week?week:en.Start.Date;day<end&&day<week.AddDays(7);day=day.AddDays(1)) {
    var slice=en.Copy();slice.Start=en.Start>day?en.Start:day;DateTime until=end<day.AddDays(1)?end:day.AddDays(1);slice.Minutes=(int)Math.Ceiling((until-slice.Start).TotalMinutes);slice.ReadOnlyReason="日をまたぐ実績";result.Add(slice);
   }
  }
  return result;
 }
}
