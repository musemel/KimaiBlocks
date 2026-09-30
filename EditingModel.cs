using System;
using System.Collections.Generic;
using System.Linq;

public static class EditingModel {
 public static string Key(Entry e){if(string.IsNullOrEmpty(e.LocalKey))e.LocalKey=e.RemoteId>0?"remote:"+e.RemoteId:Guid.NewGuid().ToString("N");return e.LocalKey;}
 public static List<Entry> Snapshot(IEnumerable<Entry> entries)=>entries.Select(e=>{Key(e);return e.Copy();}).ToList();
 // Rebuild the queue against the last known server state, including already saved edits.
 public static void Restore(State state,List<Entry> target) {
  var baseline=Snapshot(state.Entries).Where(e=>e.RemoteId>0).ToDictionary(Key);
  foreach(var p in state.Pending){baseline.Remove(Key(p.Desired));if(p.Original!=null){var old=p.Original.Copy();old.LocalKey=Key(p.Desired);baseline[old.LocalKey]=old;}}
  var restored=new List<Entry>();var pending=new List<PendingChange>();
  foreach(var old in target) {
   var desired=old.Copy();string key=Key(desired);
   if(baseline.TryGetValue(key,out var original)) {
    desired.RemoteId=original.RemoteId;desired.Fingerprint=original.Fingerprint;desired.ReadOnlyReason=original.ReadOnlyReason;desired.Billable=original.Billable;
    if(!Blocks.SameValues(desired,original))PendingQueue.Edit(pending,desired,original);
    baseline.Remove(key);
   }else {desired.RemoteId=0;desired.Fingerprint=null;desired.ReadOnlyReason=null;PendingQueue.Edit(pending,desired,null);}
   restored.Add(desired);
  }
  foreach(var old in baseline.Values)PendingQueue.Delete(pending,old);
  state.Entries=restored;state.Pending=pending;
 }
 public static IEnumerable<Entry> Forbidden(CalendarRules rules,DateTime start,DateTime end) {
  if(!rules.BlockInput)yield break;
  var spans=rules.Intervals();var holidays=rules.HolidayDates();
  for(var day=start.Date;day<end;day=day.AddDays(1)) {
   if((rules.DaysOff??Array.Empty<int>()).Contains((int)day.DayOfWeek)||holidays.Contains(day)){yield return new Entry {Start=day,Minutes=1440};continue;}
   foreach(var span in spans)yield return new Entry {Start=day.AddMinutes(span.Start),Minutes=span.End-span.Start};
  }
 }
 public static List<Entry> Plan(Entry desired,IEnumerable<Entry> blockers,CalendarRules rules) {
  return BlockOperations.Split(desired,blockers.Concat(Forbidden(rules,desired.Start,desired.Start.AddMinutes(desired.Minutes))));
 }
 public static string[] Path(string value)=>(value??"").Split('/').Select(p=>p.Trim()).Where(p=>p.Length>0).ToArray();
 public static void Tests() {
  var t=new DateTime(2026,10,1,11,30,0);var rules=new CalendarRules {BlockInput=true};var e=new Entry {Start=t,Minutes=120,LocalKey="one",RemoteId=1,Note="a"};
  var pieces=Plan(e,Array.Empty<Entry>(),rules);if(pieces.Count!=2||pieces.Sum(p=>p.Minutes)!=60||e.Minutes!=120)throw new Exception("Schedule clipping changed source or kept break");
  var state=new State {Entries=new List<Entry>{e}};var before=Snapshot(state.Entries);e.Note="saved";e.Fingerprint="new-server-version";
  Restore(state,before);if(state.Pending.Single().Original.Note!="saved"||state.Entries.Single().Fingerprint!="new-server-version"||state.Entries.Single().Note!="a")throw new Exception("Undo after save lost baseline");
  Restore(state,new List<Entry>());if(!state.Pending.Single().Delete)throw new Exception("Undo saved create must delete");
  state=new State();Restore(state,before);if(state.Pending.Single().Desired.RemoteId!=0)throw new Exception("Undo saved delete must recreate");
  if(Path("aaa/xxx").Length!=2)throw new Exception("Hierarchy parsing");
 }
}

