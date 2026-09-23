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
 public static void Tests() {
  var start=new DateTime(2026,9,21,9,0,0);var a=new Entry {Start=start,Minutes=180,RemoteId=7,LocalKey="old",Fingerprint="version",Note="memo",Billable=true};
  var pieces=Split(a,new[]{new Entry {Start=start.AddHours(1),Minutes=60}});
  if(pieces.Count!=2||pieces[0].Minutes!=60||pieces[1].Start!=start.AddHours(2)||pieces[1].Minutes!=60)throw new Exception("Split failed");
  var copy=CopyAsNew(a);if(copy.RemoteId!=0||copy.LocalKey!=null||copy.Fingerprint!=null||copy.Note!="memo"||!copy.Billable||a.RemoteId!=7)throw new Exception("Copy identity failed");
  if(Split(a,new[]{a}).Count!=0)throw new Exception("Fully occupied interval failed");
  pieces=Split(a,new[]{new Entry {Start=start.AddMinutes(61),Minutes=3},new Entry {Start=start.AddMinutes(62),Minutes=8}});
  if(pieces.Sum(e=>e.Minutes)!=170||pieces[1].Start!=start.AddMinutes(70))throw new Exception("Partial-slot exclusion failed");
  if(Split(a,new[]{new Entry {Start=start.AddMinutes(180),Minutes=5}}).Single().Minutes!=180)throw new Exception("Adjacent interval failed");
 }
}
public partial class Blocks {
 bool dragActive;
 async Task FinishBlockDrag(Entry original,Entry before,Entry desired,bool copy,int mode) {
  if(copy) {
   var duplicate=BlockOperations.CopyAsNew(desired);
   if(!AcceptSchedule(duplicate,null)){Render();return;}
   state.Entries.Add(duplicate);selected=duplicate;await CommitEntry(duplicate,null);return;
  }
  bool extends=mode!=0&&(desired.Start<before.Start||desired.Start.AddMinutes(desired.Minutes)>before.Start.AddMinutes(before.Minutes));
  if(!extends){RestoreEntry(original,desired);await CommitEntry(original,before);return;}
  var pieces=BlockOperations.Split(desired,state.Entries.Where(e=>!ReferenceEquals(e,original)));
  if(pieces.Count==0){MessageBox.Show(this,"伸ばした範囲に空き時間がないため変更できません。","自動分割");Render();return;}
  if(pieces.Any(p=>!AcceptSchedule(p,null))){Render();return;}
  RestoreEntry(original,pieces[0]);PendingQueue.Edit(state.Pending,original,before);
  foreach(var piece in pieces.Skip(1)){state.Entries.Add(piece);PendingQueue.Edit(state.Pending,piece,null);}
  selected=original;Save();Render();status.Text=pieces.Count>1?pieces.Count+" 個に分割しました · 定期保存待ち":"時間を変更しました · 定期保存待ち";
 }
}
