using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Serialization.Json;

public static class PendingQueue {
 public static void Edit(List<PendingChange> queue,Entry entry,Entry before) {
  if(entry.LocalKey==null)entry.LocalKey=Guid.NewGuid().ToString("N");
  var pending=queue.FirstOrDefault(p=>p.Desired.LocalKey==entry.LocalKey);
  if(pending==null)queue.Add(new PendingChange {Desired=entry,Original=before?.RemoteId>0?before:null});
  else pending.Desired=entry;
 }
 public static void Delete(List<PendingChange> queue,Entry entry) {
  var pending=queue.FirstOrDefault(p=>p.Desired==entry||entry.LocalKey!=null&&p.Desired.LocalKey==entry.LocalKey);
  if(pending!=null){if(pending.Original==null)queue.Remove(pending);else pending.Delete=true;}
  else {if(entry.LocalKey==null)entry.LocalKey=Guid.NewGuid().ToString("N");queue.Add(new PendingChange {Desired=entry,Original=entry.Copy(),Delete=true});}
 }
 public static void Tests() {
  var queue=new List<PendingChange>();var created=new Entry {Start=new DateTime(2026,9,21,9,0,0),Minutes=5};
  Edit(queue,created,null);
  for(int i=0;i<20;i++){var before=created.Copy();created.Minutes+=5;Edit(queue,created,before);}
  if(queue.Count!=1||queue[0].Original!=null||queue[0].Desired.Minutes!=105)throw new Exception("New edits did not coalesce");
  Delete(queue,created);if(queue.Count!=0)throw new Exception("Unsaved create/delete should require no request");
  var remote=new Entry {Start=new DateTime(2026,9,21,9,0,0),RemoteId=42,Fingerprint="original",Minutes=5};var original=remote.Copy();remote.Minutes=10;Edit(queue,remote,original);
  var prior=remote.Copy();remote.Minutes=15;Edit(queue,remote,prior);
  if(queue.Count!=1||queue[0].Original.Minutes!=5||queue[0].Desired.Minutes!=15)throw new Exception("Lost original baseline");
  Delete(queue,remote);if(!queue[0].Delete||queue[0].Original.Fingerprint!="original")throw new Exception("Delete baseline changed");
  queue[0].Attempted=true;var state=new State {Pending=queue};
  var serializer=new DataContractJsonSerializer(typeof(State));using(var stream=new MemoryStream()){serializer.WriteObject(stream,state);stream.Position=0;state=(State)serializer.ReadObject(stream);}
  if(!state.Pending[0].Attempted||!state.Pending[0].Delete||state.Pending[0].Desired.LocalKey!=remote.LocalKey)throw new Exception("Crash recovery data lost");
  var same=remote.Copy();if(!Blocks.SameValues(same,remote))throw new Exception("Reconciliation mismatch");same.Minutes+=5;if(Blocks.SameValues(same,remote))throw new Exception("Reconciliation ignored duration");
 }
}
