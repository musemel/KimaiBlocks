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
   w.state.FixedColors[ProjectColorKey(Projects[0])]="#102030";w.RefreshColors();if(w.ColorFor(Projects[0])!="#102030"||Enumerable.Range(0,100).Select(AutoColor).Distinct().Count()!=100)throw new Exception("Project colors");
   w.Render();w.UpdateLayout();w.calendarScroll.ScrollToVerticalOffset(8*Hour);w.UpdateLayout();
   // Produce a reviewable screenshot of the real WPF layout without user/server data.
   var visual=(FrameworkElement)w.Content;var bitmap=new RenderTargetBitmap((int)visual.ActualWidth,(int)visual.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(AppContext.BaseDirectory,"interaction-preview.png")))png.Save(stream);
   var report=new ReportSnapshot {Users=new List<ReportUser>{new ReportUser {Id=1},new ReportUser {Id=2}},Records=new List<ReportRecord>{new ReportRecord {UserId=1,Begin=w.week.AddHours(23),End=w.week.AddDays(1).AddHours(1)},new ReportRecord {UserId=2,Begin=w.week,End=w.week.AddHours(1)}}};
   var filtered=MemberReports.Filter(report,new HashSet<int>{1});if(filtered.Users.Count!=1||filtered.Records.Count!=1||MemberReports.Slices(filtered.Records,w.week.AddDays(1)).Single().End!=w.week.AddDays(1).AddHours(1)||MemberReports.Filter(report,new HashSet<int>()).Records.Count!=0)throw new Exception("Member filtering or day slices");
   w.ShowMemberCalendar(w,report,dialog=>{
    var root=(System.Windows.Controls.DockPanel)dialog.Content;var toolbar=(System.Windows.Controls.WrapPanel)root.Children[0];var mode=(System.Windows.Controls.ComboBox)toolbar.Children[0];mode.SelectedIndex=1;dialog.UpdateLayout();
    var content=(FrameworkElement)dialog.Content;var shot=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);shot.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(shot));using(var file=File.Create(Path.Combine(AppContext.BaseDirectory,"member-preview.png")))encoder.Save(file);dialog.Close();
   });
   Console.WriteLine("PASS: selection, bulk/inline editing, undo/redo after save, schedule clipping, colors, zoom, member filters and cross-day slices.");
  }finally {w.closingApproved=true;w.Close();}
 }
}

