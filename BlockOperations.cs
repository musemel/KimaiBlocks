using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

public static class BlockOperations {
 public static Entry CopyAsNew(Entry source) {
  var copy=source.Copy();copy.RemoteId=0;copy.LocalKey=null;copy.Fingerprint=null;copy.ReadOnlyReason=null;return copy;
 }
 public static List<Entry> Split(Entry desired,IEnumerable<Entry> blockers) {
  var occupied=blockers.Select(b=>(Start:b.Start,End:b.Start.AddMinutes(b.Minutes))).ToList();
  var pieces=new List<Entry>();Entry current=null;
  for(var time=desired.Start;time<desired.Start.AddMinutes(desired.Minutes);time=time.AddMinutes(5)) {
   bool blocked=occupied.Any(b=>b.Start<time.AddMinutes(5)&&b.End>time);
   if(blocked){current=null;continue;}
   if(current==null){current=CopyAsNew(desired);current.Start=time;current.Minutes=0;pieces.Add(current);}current.Minutes+=5;
  }
  return pieces;
 }
 public static List<Entry> Plan(Entry desired,IEnumerable<Entry> entries,Entry original,bool copy) {
  // Moving replaces the source; copying must also avoid the source interval.
  return Split(desired,entries.Where(e=>copy||!ReferenceEquals(e,original)));
 }
 public static void Tests() {
  var start=new DateTime(2026,9,21,9,0,0);var a=new Entry {Start=start,Minutes=180,RemoteId=7,LocalKey="old",Fingerprint="version",Note="memo",Billable=true};
  var pieces=Split(a,new[]{new Entry {Start=start.AddHours(1),Minutes=60}});
  if(pieces.Count!=2||pieces[0].Minutes!=60||pieces[1].Start!=start.AddHours(2)||pieces[1].Minutes!=60)throw new Exception("Split failed");
  var copy=CopyAsNew(a);if(copy.RemoteId!=0||copy.LocalKey!=null||copy.Fingerprint!=null||copy.Note!="memo"||!copy.Billable||a.RemoteId!=7)throw new Exception("Copy identity failed");
  if(Split(a,new[]{a}).Count!=0)throw new Exception("Fully occupied interval failed");
  pieces=Split(a,new[]{new Entry {Start=start.AddMinutes(61),Minutes=3},new Entry {Start=start.AddMinutes(62),Minutes=8}});
  if(pieces.Sum(e=>e.Minutes)!=170||pieces[1].Start!=start.AddMinutes(70))throw new Exception("Partial-slot exclusion failed");
  if(Split(a,new[]{new Entry {Start=start.AddMinutes(180),Minutes=5}}).Single().Minutes!=180)throw new Exception("Adjacent interval failed");
  var source=new Entry {Start=start,Minutes=60,RemoteId=11};var target=source.Copy();target.Start=start.AddMinutes(30);
  if(Plan(target,new[]{source},source,false).Single().Minutes!=60)throw new Exception("Move must exclude source");
  pieces=Plan(target,new[]{source},source,true);
  if(pieces.Single().Start!=start.AddHours(1)||pieces[0].Minutes!=30)throw new Exception("Copy must retain source");
  var obstacle=new Entry {Start=start.AddMinutes(45),Minutes=15};
  pieces=Plan(target,new[]{source,obstacle},source,false);
  if(pieces.Count!=2||pieces[0].Minutes!=15||pieces[1].Minutes!=30||obstacle.Minutes!=15)throw new Exception("Move split failed");
  var resized=source.Copy();resized.Start=start.AddMinutes(-30);resized.Minutes=90;
  pieces=Plan(resized,new[]{source,obstacle},source,false);
  if(pieces.Single().Minutes!=75)throw new Exception("Upper-edge adjustment failed");

 }
}
public partial class Blocks {
 bool dragActive;
 async Task FinishBlockDrag(Entry original,Entry before,Entry desired,bool copy,int mode) {
  var pieces=BlockOperations.Plan(desired,state.Entries,original,copy);
  if(pieces.Count==0){MessageBox.Show(this,"操作先に空き時間がないため変更できません。元の実績はそのまま残します。","自動調整");Render();return;}
  if(pieces.Any(p=>!AcceptSchedule(p,null))){Render();return;}
  int newStart=0;
  if(!copy) {
   RestoreEntry(original,pieces[0]);PendingQueue.Edit(state.Pending,original,before);selected=original;newStart=1;
  }
  foreach(var piece in pieces.Skip(newStart)){state.Entries.Add(piece);PendingQueue.Edit(state.Pending,piece,null);}
  if(copy)selected=pieces[0];
  Save();Render();
  int removed=desired.Minutes-pieces.Sum(p=>p.Minutes);
  status.Text=(copy?"コピー":"時間変更")+" · "+pieces.Count+" 個"+(removed>0?" · 重複 "+removed+" 分を除外":"")+" · 定期保存待ち";
  await Task.CompletedTask;
 }
}
