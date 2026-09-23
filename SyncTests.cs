using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

public partial class Blocks {
 static void RunSyncTests() {
  var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
  app.Startup+=async(s,e)=>{
   string testFolder=Path.Combine(Path.GetTempPath(),"KimaiBlocks-sync-"+Guid.NewGuid().ToString("N"));
   Blocks window=null;
   try {
    window=new Blocks(true);window.Show();window.demoMode=false;window.state=new State();window.file=Path.Combine(testFolder,"cache.json");
    var mock=new MockKimai();window.service=new KimaiService("https://example.test/kimai","test-token","",false,mock);await window.service.InitializeAsync();
    Projects=window.service.Projects.Select(p=>window.service.ProjectName(p.Id.Value)).ToArray();
    int notices=0;window.testError=text=>notices++;
    Entry NewEntry()=>new Entry {ProjectId=11,ActivityId=22,Project=window.service.ProjectName(11),Activity=window.service.ActivityName(22),Start=new DateTime(2026,9,21,9,5,0),Minutes=5,Billable=true};
    var entry=NewEntry();window.state.Entries.Add(entry);await window.CommitEntry(entry,null);
    var before=entry.Copy();entry.Minutes=10;await window.CommitEntry(entry,before);
    if(mock.Methods.Any(m=>m.StartsWith("POST")))throw new Exception("Edit sent immediately");
    if(!await window.FlushAsync(false)||window.state.Pending.Count!=0||mock.Methods.Count(m=>m.StartsWith("POST"))!=1)throw new Exception("Batch save failed");
    entry=NewEntry();entry.Start=entry.Start.AddHours(1);window.state.Entries.Add(entry);await window.CommitEntry(entry,null);
    window.saveTimer.Interval=TimeSpan.FromMilliseconds(50);window.saveTimer.Start();await WaitUntil(()=>window.state.Pending.Count==0&&!window.communicating);window.saveTimer.Stop();
    if(mock.Methods.Count(m=>m.StartsWith("POST"))!=2)throw new Exception("Timer failed to flush");
    entry=NewEntry();entry.Start=entry.Start.AddHours(2);window.state.Entries.Add(entry);await window.CommitEntry(entry,null);
    mock.TimeoutWrite=true;
    if(await window.FlushAsync(false)||notices!=1||!window.savePaused||window.state.Pending.Count!=1||!window.state.Pending[0].Attempted)throw new Exception("Failure did not preserve/block");
    int sent=mock.Methods.Count(m=>m.StartsWith("POST"));await window.FlushAsync(false);
    if(mock.Methods.Count(m=>m.StartsWith("POST"))!=sent)throw new Exception("Uncertain write retried");
    // Make the failure definitive and exercise the actual async Closing event.
    window.state.Pending[0].Attempted=false;mock.TimeoutWrite=false;mock.FailWrite=true;
    window.Close();await WaitUntil(()=>!window.closingRequested&&!window.communicating);
    if(!window.IsVisible||window.closingApproved||window.state.Pending.Count!=1||notices!=2)throw new Exception("Failed save allowed close");
    window.state.Pending[0].Attempted=false;mock.FailWrite=false;
    window.Close();await WaitUntil(()=>window.closingApproved&&!window.IsVisible);
    if(window.IsVisible||window.state.Pending.Count!=0)throw new Exception("Successful close did not save");
    Projects=new[]{"A","B","C","D"};var empty=new Blocks(true);empty.Show();empty.demoMode=false;empty.state=new State();empty.file=Path.Combine(testFolder,"empty.json");empty.Close();await WaitUntil(()=>!empty.IsVisible);if(!empty.closingApproved)throw new Exception("Empty close failed");
    Console.WriteLine("PASS: batching, periodic timer, no immediate POST, cache persistence, uncertain non-retry, failure blocks close, successful close flushes.");
    app.Shutdown(0);
   }catch(Exception ex){Console.Error.WriteLine(ex);if(window!=null){window.closingApproved=true;window.Close();}app.Shutdown(1);}
   finally {if(Directory.Exists(testFolder))Directory.Delete(testFolder,true);}
  };
  int code=app.Run();Environment.ExitCode=code;
 }
 static async Task WaitUntil(Func<bool> ready) {
  var deadline=DateTime.UtcNow.AddSeconds(10);
  while(!ready()){if(DateTime.UtcNow>deadline)throw new Exception("Async lifecycle timeout");await Task.Delay(20);}
 }
}

