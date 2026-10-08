using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public partial class Blocks {
 static async Task RunInteractionChecks(string folder) {
  Projects=new[]{"P1","P2","P3","P4"};var w=new Blocks(true);w.file=Path.Combine(folder,"interaction.json");w.week=new DateTime(2026,9,21);w.state=new State();w.Show();
  try {
   w.service=new KimaiService("https://example.test/kimai","test-token","",false,new MockKimai());await w.service.InitializeAsync();Projects=w.service.Projects.Select(p=>w.service.ProjectName(p.Id.Value)).ToArray();
   Entry Make(int hour)=>new Entry {LocalKey=Guid.NewGuid().ToString("N"),RemoteId=hour,Project=Projects[0],Activity=w.service.ActivityName(22),ProjectId=11,ActivityId=22,Start=w.week.AddHours(hour),Minutes=30,Note="aaa/xxx"};
   var a=Make(9);var b=Make(10);var c=Make(11);w.state.Entries.AddRange(new[]{a,b,c});w.Render();
   w.ClearHistory();w.UpdateEditToolbar();if(w.undoButton.IsEnabled||w.redoButton.IsEnabled||w.copyButton.IsEnabled)throw new Exception("Empty toolbar state");
   w.SelectEntry(a,ModifierKeys.None);w.copyButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));if(!w.pasteButton.IsEnabled||w.clipboardEntries.Count!=1)throw new Exception("Toolbar copy");
   w.pasteTime=w.week.AddHours(15);await w.PasteSelection();if(!w.undoButton.IsEnabled||w.state.Entries.Count!=4)throw new Exception("Toolbar paste history");
   if(w.SelectedEntries().Count!=0||w.selected!=null)throw new Exception("Copy did not clear selection");
   w.undoButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));if(w.state.Entries.Count!=3||!w.redoButton.IsEnabled)throw new Exception("Toolbar undo");
   w.redoButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));if(w.state.Entries.Count!=4)throw new Exception("Toolbar redo");w.UndoEdit();
   a=w.state.Entries.Single(e=>e.Start.Hour==9);b=w.state.Entries.Single(e=>e.Start.Hour==10);c=w.state.Entries.Single(e=>e.Start.Hour==11);
   w.statisticsTabs.SelectedIndex=1;w.statisticsDay=w.week;w.RenderStatistics(w.VisibleEntries());w.UpdateLayout();
   if(!w.statisticsTabs.IsVisible||((System.Windows.Controls.TabItem)w.statisticsTabs.Items[1]).Content is not System.Windows.Controls.ScrollViewer)throw new Exception("Statistics tabs not attached");
   var dailyTree=w.dayStatistics.Children.OfType<System.Windows.Controls.TreeView>().Single();var projectNode=(System.Windows.Controls.TreeViewItem)dailyTree.Items[0];var commentNode=(System.Windows.Controls.TreeViewItem)projectNode.Items[0];
   if(!projectNode.IsExpanded||!commentNode.IsExpanded||commentNode.Items.Count!=1)throw new Exception("Daily hierarchy not expanded");
   projectNode.IsExpanded=false;w.RenderStatistics(w.VisibleEntries());if(((System.Windows.Controls.TreeViewItem)w.dayStatistics.Children.OfType<System.Windows.Controls.TreeView>().Single().Items[0]).IsExpanded)throw new Exception("Daily collapse lost");w.collapsedDailyNodes.Clear();w.RenderStatistics(w.VisibleEntries());
   w.SelectEntry(a,ModifierKeys.None);w.SelectEntry(c,ModifierKeys.Shift);if(w.SelectedEntries().Count!=3)throw new Exception("Shift range selection");w.SelectEntry(b,ModifierKeys.Control);if(w.SelectedEntries().Count!=2)throw new Exception("Ctrl toggle selection");
   w.editorComment.Text="bulk comment";if(!w.ApplyEditor()||w.state.Pending.Count!=2||b.Note!="aaa/xxx")throw new Exception("Bulk comment editor");w.UndoEdit();if(w.state.Pending.Count!=0||w.state.Entries.Any(e=>e.Note!="aaa/xxx"))throw new Exception("Bulk undo");
   a=w.state.Entries.Single(e=>e.Start.Hour==9);b=w.state.Entries.Single(e=>e.Start.Hour==10);c=w.state.Entries.Single(e=>e.Start.Hour==11);
   w.settings.Calendar.BlockInput=true;var desired=a.Copy();desired.Start=w.week.AddHours(11).AddMinutes(30);desired.Minutes=120;await w.ApplyBatchDrag(new List<Entry>{a},new List<Entry>{desired},false);
   if(w.state.Entries.Count!=4||a.Minutes!=30||b.Start.Hour!=10||c.Start.Hour!=11||w.state.Entries.Any(e=>w.Rules.Intersects(e.Start,e.Minutes)))throw new Exception("Selective schedule clipping");
   var copy=a.Copy();copy.Start=w.week.AddHours(14);int count=w.state.Entries.Count;await w.ApplyBatchDrag(new List<Entry>{a},new List<Entry>{copy},true);if(w.state.Entries.Count!=count+1||a.Start.Hour!=11)throw new Exception("Copy changed original");w.UndoEdit();a=w.state.Entries.Single(e=>e.Start.Hour==11&&e.Start.Minute==30);
   var inBreak=new Entry {LocalKey="break",RemoteId=77,Start=w.week.AddHours(12),Minutes=30,Project=Projects[0],Activity=w.service.ActivityName(22),ProjectId=11,ActivityId=22};w.state.Entries.Add(inBreak);var baseline=inBreak.Copy();inBreak.Note="metadata only";await w.CommitEntry(inBreak,baseline);if(inBreak.Start.Hour!=12||inBreak.Minutes!=30)throw new Exception("Existing forbidden-time record modified");
   w.BeginInlineComment(a);if(w.inlineComment==null)throw new Exception("Inline editor missing");w.inlineComment.Text="inline comment";w.FinishInline(true);if(a.Note!="inline comment")throw new Exception("Inline comment commit");
   w.SetZoom(200);if(Hour!=288||w.board.Height!=6912)throw new Exception("Zoom scale");w.SetZoom(100);
   if(TimeLabelStep(72)!=30||TimeLabelStep(144)!=10||TimeLabelStep(288)!=5)throw new Exception("Adaptive time labels");
   w.PreviewZoom(120,100);if(w.zoomScale<=1)throw new Exception("Zoom preview missing");w.SetZoom(100);if(w.zoomScale!=1)throw new Exception("Zoom preview not committed");
   w.settings.ProjectPalette=0;w.settings.AvoidColorCollisions=true;w.colorSignature="";w.RefreshColors();var stable=w.ColorFor(Projects[0]);var otherColor=w.ColorFor(Projects[1]);if(stable==otherColor)throw new Exception("Unlimited color collision");w.state.Hidden.Add(Projects[0]);w.RefreshColors();if(w.ColorFor(Projects[0])!=stable||w.ColorFor(Projects[1])!=otherColor)throw new Exception("Visibility changed stable color");w.state.Hidden.Clear();w.settings.ProjectPalette=16;w.settings.AvoidColorCollisions=false;w.colorSignature="";
   w.state.Folders.AddRange(new[]{"parent","child","favorites"});w.state.FolderLabels["parent"]="業務";w.state.FolderLabels["child"]="一部の作業";w.state.FolderLabels["favorites"]="お気に入り";w.state.FolderParents["child"]="parent";w.state.FolderWorks["child"]=new List<WorkLink>{new WorkLink {Project=Projects[0],Activity=w.service.ActivityName(22)}};w.state.FavoriteFolders.Add("favorites");w.state.Favorites.Add(Projects[0]+"|"+w.service.ActivityName(22));
   if(w.MoveFolder("parent","child"))throw new Exception("Folder cycle allowed");w.state.FolderLabels["parent"]="業務（変更後）";if(w.ParentFolder("child")!="parent")throw new Exception("Rename changed hierarchy");w.state.RemoveFolder("parent");if(w.ParentFolder("child")!=null||w.state.FolderWorks["child"].Count!=1)throw new Exception("Folder deletion lost child");
   w.Populate();var pinned=(System.Windows.Controls.TreeViewItem)w.tree.Items[0];if(((System.Windows.Controls.TextBlock)pinned.Header).Text!="★ お気に入り"||pinned.ContextMenu!=null||w.state.FavoriteFolders.Count!=0)throw new Exception("Pinned favorites migration");var rootNode=(System.Windows.Controls.TreeViewItem)w.tree.Items[1];if(rootNode.Items.OfType<System.Windows.Controls.TreeViewItem>().Any(n=>n.IsExpanded))throw new Exception("Projects should start collapsed");
   w.UpdateLayout();var projectNodeForLayout=rootNode.Items.OfType<System.Windows.Controls.TreeViewItem>().First();var projectHeader=(FrameworkElement)projectNodeForLayout.Header;var headerBefore=projectHeader.TransformToAncestor(w.tree).Transform(new Point());projectNodeForLayout.IsExpanded=true;w.UpdateLayout();var headerAfter=projectHeader.TransformToAncestor(w.tree).Transform(new Point());var firstChild=(System.Windows.Controls.TreeViewItem)projectNodeForLayout.Items[0];var childPosition=((FrameworkElement)firstChild.Header).TransformToAncestor(w.tree).Transform(new Point());if((headerAfter-headerBefore).Length>.1||childPosition.X<=headerAfter.X||childPosition.Y<=headerAfter.Y)throw new Exception("Tree expansion moved header or failed indentation");projectNodeForLayout.IsExpanded=false;
   var searchable=new ProjectSearchBox(new[]{"ガラス　テスト [#1]","別の案件 [#2]"},"別の案件 [#2]",Matches);int changes=0;searchable.SelectionChanged+=(s,e)=>changes++;searchable.FilterQuery("がらすﾃｽﾄ");if(searchable.Items.Count!=1||searchable.SelectedItem!=null||changes!=0)throw new Exception("Project fuzzy query modified selection");searchable.SelectedItem=searchable.Items[0];if(changes!=1)throw new Exception("Project result did not commit");searchable.FilterQuery("見つからない");if(searchable.Items.Count!=0||changes!=1)throw new Exception("No-result project search");
   w.state.Folders.Add("order-test");if(!w.MoveFolderRelative("order-test","child",false)||w.state.Folders.IndexOf("order-test")>=w.state.Folders.IndexOf("child"))throw new Exception("Folder drop ordering");if(!w.MoveFolder("order-test","child")||w.MoveFolderRelative("child","order-test",true))throw new Exception("Folder drop cycle");w.state.RemoveFolder("order-test");
   w.state.FixedColors[ProjectColorKey(Projects[0])]="#102030";w.RefreshColors();if(w.ColorFor(Projects[0])!="#102030"||Enumerable.Range(0,100).Select(AutoColor).Distinct().Count()!=100)throw new Exception("Project colors");
   w.SelectEntry(a,ModifierKeys.None);w.SetDayView(true);w.UpdateLayout();if(w.DisplayDayCount!=1||w.DisplayStart!=a.Start.Date||w.entryBoxes.Values.Any(box=>box.Effect!=null))throw new Exception("Day view / clear selected text");
   await w.NavigateCalendar(1);if(w.DisplayStart!=w.week.AddDays(1)||w.entryBoxes.Count!=0)throw new Exception("Day navigation");w.SetDayView(false);if(w.DisplayDayCount!=5)throw new Exception("Week view");
   w.rangeStart=w.week.AddDays(2).AddHours(10);w.rangeMinutes=90;w.DrawSelectedRange();if(w.rangeVisual==null||Math.Abs(w.rangeVisual.Height-1.5*Hour)>.01)throw new Exception("Selected time range");w.rangeStart=null;
   var hiddenProject=w.service.Projects.First(p=>p.Id==a.ProjectId);hiddenProject.Visible=false;if(w.VisibleEntries().Any(e=>e.ProjectId==a.ProjectId))throw new Exception("Disabled project calendar");hiddenProject.Visible=true;
   w.BeginInlineComment(a);w.inlineComment.Text="Enter confirms";w.inlineComment.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(w),0,Key.Enter) {RoutedEvent=Keyboard.PreviewKeyDownEvent});if(w.inlineComment!=null||a.Note!="Enter confirms")throw new Exception("F2 Enter confirmation");
   void CaptureDialog(string title,string name,Action open){w.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(()=>{var dialog=w.OwnedWindows.Cast<Window>().Single(x=>x.Title==title);dialog.UpdateLayout();var content=(FrameworkElement)dialog;var shot=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);shot.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(shot));using(var file=File.Create(Path.Combine(AppContext.BaseDirectory,name)))encoder.Save(file);dialog.Close();}));open();}
   CaptureDialog("プロジェクトの固定色","color-picker-preview.png",()=>w.ColorDialog(Projects[0]));
   string ownPattern=w.settings.ActivityGroupingPattern,ownFolder=w.settings.UpdateFolder;w.managedSettings=ManagedSettings.Read("{\"ActivityGroupingPattern\":\"(.*):(.*)\",\"UpdateFolder\":\"managed-folder\"}");w.ApplyManagedSettings();if(w.EffectiveActivityPattern!="(.*):(.*)"||w.EffectiveUpdateFolder!="managed-folder"||!w.Locked("ActivityGroupingPattern"))throw new Exception("Managed override not applied");w.managedSettings=new ManagedSettings();w.settings.ActivityGroupingPattern=ownPattern;w.settings.UpdateFolder=ownFolder;
   CaptureDialog("表示・作業ツリー","view-settings-preview.png",w.ViewSettingsDialog);
   foreach(var activity in w.service.Activities)activity.Name="開発作業/仕様の確認と画面の表示調整を行う長いアクティビティ名";w.state.Hidden.Clear();w.state.Favorites.Clear();w.state.Favorites.Add(Projects[0]+"|"+w.service.ActivityName(22));w.state.FolderWorks["child"][0].Activity=w.service.ActivityName(22);w.state.ExpandedNodes.AddRange(new[]{"project:"+Projects[0],"activity-regex:"+Projects[0]+"/開発作業","folder:child","folder:favorites"});w.Populate();w.Render();w.UpdateLayout();w.calendarScroll.ScrollToVerticalOffset(8*Hour);w.UpdateLayout();
   if(w.editorProject.SelectedItem==null||w.editorProject.Text!=(string)w.editorProject.SelectedItem)throw new Exception("Project picker lost current value");
   // Produce a reviewable screenshot of the real WPF layout without user/server data.
   var visual=(FrameworkElement)w.Content;var bitmap=new RenderTargetBitmap((int)visual.ActualWidth,(int)visual.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(AppContext.BaseDirectory,"interaction-preview.png")))png.Save(stream);
   var report=new ReportSnapshot {Users=new List<ReportUser>{new ReportUser {Id=1},new ReportUser {Id=2}},Records=new List<ReportRecord>{new ReportRecord {UserId=1,Begin=w.week.AddHours(23),End=w.week.AddDays(1).AddHours(1)},new ReportRecord {UserId=2,Begin=w.week,End=w.week.AddHours(1)}}};
   var filtered=MemberReports.Filter(report,new HashSet<int>{1});if(filtered.Users.Count!=1||filtered.Records.Count!=1||MemberReports.Slices(filtered.Records,w.week.AddDays(1)).Single().End!=w.week.AddDays(1).AddHours(1)||MemberReports.Filter(report,new HashSet<int>()).Records.Count!=0)throw new Exception("Member filtering or day slices");
   w.ShowMemberCalendar(w,report,dialog=>{
    var root=(System.Windows.Controls.DockPanel)dialog.Content;var toolbar=(System.Windows.Controls.WrapPanel)root.Children[0];var mode=(System.Windows.Controls.ComboBox)toolbar.Children[0];mode.SelectedIndex=1;dialog.UpdateLayout();
    var content=(FrameworkElement)dialog;var shot=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);shot.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(shot));using(var file=File.Create(Path.Combine(AppContext.BaseDirectory,"member-preview.png")))encoder.Save(file);dialog.Close();
   });
   Console.WriteLine("PASS: selection, bulk/inline editing, undo/redo after save, schedule clipping, colors, zoom, member filters and cross-day slices.");
  }finally {w.closingApproved=true;w.Close();}
 }
}





