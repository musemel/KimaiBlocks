using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

public partial class Blocks {
 static async Task RunUsabilityChecks(string folder) {
  Projects=new[]{"A","B","C","D"};var w=new Blocks(true);w.file=Path.Combine(folder,"usability.json");w.week=new DateTime(2026,9,21);w.state=new State();w.Show();
  try {
   var mock=new MockKimai();w.service=new KimaiService("https://example.test/kimai","test-token","",false,mock);await w.service.InitializeAsync();Projects=w.service.Projects.Select(p=>w.service.ProjectName(p.Id.Value)).ToArray();w.Render();w.Populate();
   void Check(bool result,string message){if(!result)throw new Exception(message);}
   var entry=new Entry {LocalKey="source",RemoteId=88,ProjectId=11,ActivityId=22,Project=Projects[0],Activity=w.service.ActivityName(22),Start=w.week.AddHours(9),Minutes=120,Note="定例会議 / 共有"};w.state.Entries.Add(entry);
   w.RecordInput(entry);w.RecordInput(entry);Check(w.state.InputHistory.Count==1&&w.state.InputHistory[0].RemoteId==0&&entry.RemoteId==88,"Reuse deduplication / identity");
   for(int i=0;i<110;i++){var value=entry.Copy();value.Note="履歴 "+i;w.RecordInput(value);}Check(w.state.InputHistory.Count==100&&w.state.InputHistory[0].Note=="履歴 109","Reuse history limit / order");w.RecordInput(entry);
   var reusable=w.state.InputHistory[0];var drag=new DataObject("saved-input",System.Text.Json.JsonSerializer.Serialize(reusable,new System.Text.Json.JsonSerializerOptions {IncludeFields=true}));Check(IsWorkDrop(drag)&&DroppedInput(drag).Note==entry.Note&&w.ResolveDroppedWork(drag)[1]==entry.Activity,"Reuse drag payload");
   w.SeedInputHistory(new[]{new Entry {Project=entry.Project,Activity=entry.Activity,Minutes=30,Note="server"}});Check(ReferenceEquals(w.state.InputHistory[0],reusable),"Reload reordered local history");
   w.state.InputTemplates.Add(new SavedInput {Name="朝会",Value=reusable.Copy()});w.RefreshReuse();Check(w.historyInputs.Items.Count==100&&w.templateInputs.Items.Count==1,"Reuse tabs not populated");
   w.state.Hidden.Add(Projects[0]);w.PopulateProjectList();Check(w.projectList.Items.Cast<ProjectChoice>().First().Name==Projects[1],"Checked projects not sorted first");w.state.Hidden.Clear();
   w.state.Favorites.Add(entry.Project+"|"+entry.Activity);w.state.ExpandedNodes.Add("project:"+entry.Project);w.state.Collapsed.Add("root");w.Populate();w.favorites.IsChecked=true;w.Populate();Check(!((TreeViewItem)w.tree.Items[1]).IsExpanded&&w.state.ExpandedNodes.Contains("project:"+entry.Project),"Favorites filter lost expansion");w.favorites.IsChecked=false;w.Populate();Check(!((TreeViewItem)w.tree.Items[1]).IsExpanded,"Favorites filter expanded root");
   w.SetZoom(200);w.Render();w.UpdateLayout();w.calendarScroll.ScrollToVerticalOffset(8*Hour);w.UpdateLayout();var box=w.entryBoxes[entry];var point=new Point(Canvas.GetLeft(box)+box.Width/2,Canvas.GetTop(box)+box.Height*.75);Check(ReferenceEquals(w.board.InputHitTest(point),box),"Lower block area failed hit test at 200%");
   w.PreviewZoom(120,100);w.UpdateLayout();await Dispatcher.Yield(DispatcherPriority.Render);w.UpdateLayout();box=w.entryBoxes[entry];
   box.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left) {RoutedEvent=UIElement.MouseLeftButtonDownEvent});Check(w.IsSelected(entry),"Lower-block selection handler");
   // Native mouse capture is unavailable on some headless test desktops. Exercise the
   // gesture guard independently of capture, then release before committing the zoom.
   w.dragActive=true;await Task.Delay(180);Check(w.entryBoxes[entry]==box,"Zoom timer replaced active gesture visual");w.dragActive=false;if(box.IsMouseCaptured)box.ReleaseMouseCapture();w.SetZoom(100);
   Check(ValidDisplayRange(w.week,w.week.AddDays(30))&&!ValidDisplayRange(w.week,w.week.AddDays(31))&&!ValidDisplayRange(w.week,w.week.AddDays(-1)),"Display range bounds");
   await w.ChangeRange(w.week.AddDays(2),w.week.AddDays(32),210);w.UpdateLayout();Check(w.DisplayDayCount==31&&w.DisplayStart==new DateTime(2026,9,23)&&Math.Abs(w.DayWidth-210)<.1&&w.calendarScroll.ScrollableWidth>1000,"Custom range width / horizontal scroll");
   int beforeWidthChange=mock.Methods.Count;await w.ChangeRange(w.customFrom.Value,w.customThrough.Value,220);w.UpdateLayout();Check(mock.Methods.Count==beforeWidthChange&&Math.Abs(w.DayWidth-220)<.1,"Width-only edit made network requests");
   string query=Uri.UnescapeDataString(mock.Queries.Last());Check(query.Contains("begin=2026-09-21T00:00:00")&&query.Contains("end=2026-10-25T23:59:59")&&query.Contains("user=7"),"Range API boundaries / owner");
   var last=entry.Copy();last.LocalKey="last";last.RemoteId=0;last.Start=w.customThrough.Value.AddHours(9);w.state.Entries.Add(last);w.Render();w.UpdateLayout();Check(w.entryBoxes.ContainsKey(last),"Last date omitted from range");w.calendarScroll.ScrollToHorizontalOffset(500);w.UpdateLayout();Check(Math.Abs(w.headerScroll.HorizontalOffset-500)<1,"Header scroll not synchronized");
   w.statisticsDay=last.Start.Date;w.RenderStatistics(w.VisibleEntries());Check(w.StatisticsWeek==Monday(last.Start)&&((TextBlock)w.weekStatistics.Children[1]).Text==Hours(last.Minutes),"Weekly totals mixed multiple weeks");
   w.Persist();using(var f=File.OpenRead(w.file)){var restored=(State)new DataContractJsonSerializer(typeof(State)).ReadObject(f);Check(restored.CachedFrom==w.customFrom&&restored.CachedThrough==w.customThrough&&restored.InputHistory.Count>0&&restored.InputTemplates.Single().Name=="朝会","Range and reuse cache persistence");}
   last.RemoteId=91; // Treat the range fixture as a server record for undo baseline checks.
   var prior=entry.Copy();prior.Start=w.week.AddDays(-7).AddHours(9);prior.Minutes=120;var prior2=prior.Copy();prior2.RemoteId=89;prior2.LocalKey="prior2";prior2.Start=prior.Start.AddHours(2.5);prior2.Minutes=30;
   Check(w.CopyComparisonEntries(new[]{prior2,prior})&&w.clipboardEntries.Count==2&&w.clipboardEntries.All(e=>e.RemoteId==0&&e.LocalKey==null),"Comparison copy identity/order");
   bool oldLock=w.Rules.BlockInput;w.Rules.BlockInput=true;var occupied=entry.Copy();occupied.LocalKey="paste-obstacle";occupied.RemoteId=90;occupied.Start=w.DisplayStart.AddHours(14).AddMinutes(5);occupied.Minutes=5;w.state.Entries.Add(occupied);int originalCount=w.state.Entries.Count;
   w.pasteTime=w.DisplayStart.AddHours(11.5);await w.PasteSelection();var additions=w.state.Entries.Where(e=>e.Start.Date==w.DisplayStart&&e.LocalKey!="paste-obstacle").ToList();Check(additions.Count==4&&additions.Sum(e=>e.Minutes)==85&&additions.All(e=>e.RemoteId==0&&e.Note==entry.Note&&!w.Rules.Intersects(e.Start,e.Minutes)),"Comparison paste did not clip breaks/overlap or preserve comments");Check(prior.RemoteId==88&&prior.Start==w.week.AddDays(-7).AddHours(9)&&prior.Minutes==120,"Copy modified prior week");
   w.UndoEdit();Check(w.state.Entries.Count==originalCount&&w.state.Pending.Count==0,"Comparison paste undo");w.state.Entries.RemoveAll(e=>e.LocalKey=="paste-obstacle");w.Rules.BlockInput=oldLock;w.ClearHistory();
   var invalid=prior.Copy();invalid.Minutes=7;var kept=w.clipboardEntries;Check(!w.CopyComparisonEntries(new[]{invalid})&&ReferenceEquals(kept,w.clipboardEntries),"Invalid copy replaced clipboard");
   void Capture(Window window,string name){window.UpdateLayout();var bmp=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(Path.Combine(AppContext.BaseDirectory,name)))png.Save(f);}
   w.workTabs.SelectedIndex=1;w.calendarScroll.ScrollToHorizontalOffset(0);w.calendarScroll.ScrollToVerticalOffset(8*Hour);Capture(w,"range-history-preview.png");
   void Dialog(string name,Action show,Action<Window> inspect=null){w.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>{var dialog=w.OwnedWindows.Cast<Window>().Single();dialog.UpdateLayout();inspect?.Invoke(dialog);Capture(dialog,name);dialog.Close();}));show();}
   Dialog("comparison-preview.png",()=>w.ShowWeekComparison(w.week,new List<Entry>{new Entry {Project=entry.Project,Activity=entry.Activity,ProjectId=11,ActivityId=22,Start=w.week.AddDays(-7).AddHours(9),Minutes=60,Note="先週の会議"}},new List<Entry>{entry}),dialog=>{IEnumerable<DependencyObject> Descendants(DependencyObject node){for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++){var child=VisualTreeHelper.GetChild(node,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}}var sourceBox=Descendants(dialog).OfType<Border>().Single(b=>b.Tag is Entry);sourceBox.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left) {RoutedEvent=UIElement.MouseLeftButtonDownEvent});var copyButton=Descendants(dialog).OfType<Button>().Single(b=>b.Content as string=="コピー（Ctrl+C）");Check(copyButton.IsEnabled&&sourceBox.BorderThickness.Left==3,"Comparison selection affordance");copyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(w.clipboardEntries.Single().Note=="先週の会議","Comparison copy button");});
   Dialog("range-dialog-preview.png",w.SelectRange);Dialog("update-preview.png",()=>w.ShowUpdateDialog(folder,new Version(2099,1,1,0)));
   Console.WriteLine("PASS: history/templates, bounds and cache, checked sorting, favorite expansion, lower-block hit testing during zoom, 31-day scrolling, range API dates, weekly totals and comparison/update dialogs.");
  }finally {w.closingApproved=true;w.Close();}
 }
}
