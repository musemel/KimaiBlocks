using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

public partial class Blocks {
 readonly Dictionary<Entry,Border> entryBoxes=new Dictionary<Entry,Border>();
 internal static string BlockCaption(Entry e)=>e.Project+(string.IsNullOrWhiteSpace(e.Note)?"":"\n"+e.Note.Trim())+"\n"+BlockTime(e)+"\n"+e.Activity;
 ToolTip EntryTip(Entry en)=>new ToolTip {Content=new TextBlock {Text=en.Project+"\n"+en.Activity+"\n"+en.Start.ToString("yyyy/MM/dd ")+BlockTime(en)+"\n\n"+(en.Note??"")+"\n\n"+(en.ReadOnlyReason??"Ctrl+ドラッグ: コピー / Ctrl・Shift+クリック: 複数選択\nF2: コメント編集 / ダブルクリック: 詳細編集")+(en.RemoteId>0?"\n実績ID: "+en.RemoteId:"\n未保存の実績"),MaxWidth=460,TextWrapping=TextWrapping.Wrap}};
 void DrawEntry(Entry en,List<Entry> entries) {
  int day=(en.Start.Date-DisplayStart).Days;
  var ordered=entries.Where(x=>x.Start.Date==en.Start.Date).OrderBy(x=>x.Start).ThenBy(x=>x.Minutes).ToList();var group=new List<Entry>();DateTime end=DateTime.MinValue;
  foreach(var x in ordered){if(x.Start>=end&&group.Count>0){if(group.Contains(en))break;group.Clear();end=DateTime.MinValue;}group.Add(x);if(x.Start.AddMinutes(x.Minutes)>end)end=x.Start.AddMinutes(x.Minutes);}
  var lanes=new List<DateTime>();int lane=0;foreach(var x in group){int n=lanes.FindIndex(t=>t<=x.Start);if(n<0){n=lanes.Count;lanes.Add(DateTime.MinValue);}lanes[n]=x.Start.AddMinutes(x.Minutes);if(x==en)lane=n;}
  double width=(DayWidth-6)/Math.Max(1,lanes.Count);
  var box=new Border {Width=Math.Max(12,width-3),Height=en.Minutes/60.0*Hour,Background=BrushOf(ColorFor(en.Project)),BorderBrush=IsSelected(en)?BrushOf("#064FA3"):Brushes.White,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4),ClipToBounds=true,ToolTip=EntryTip(en),AllowDrop=true};
  var text=Label(BlockCaption(en),en.Minutes<=10?9:11);text.Foreground=ReadableText(ColorFor(en.Project));text.VerticalAlignment=VerticalAlignment.Top;text.Margin=new Thickness(3,0,2,0);text.LineStackingStrategy=LineStackingStrategy.BlockLineHeight;text.LineHeight=en.Minutes<=10?10:14;text.TextWrapping=TextWrapping.Wrap;text.TextTrimming=TextTrimming.CharacterEllipsis;text.IsHitTestVisible=false;box.Child=text;
  if(!string.IsNullOrWhiteSpace(en.Note)){text.Text=en.Project+"\n"+BlockTime(en)+"\n"+en.Activity+"\n"+en.Note.Trim();text.Measure(new Size(Math.Max(1,box.Width-9),double.PositiveInfinity));if(text.DesiredSize.Height>box.Height-4)text.Text=BlockCaption(en);}
  Canvas.SetLeft(box,Gutter+day*DayWidth+3+lane*width);Canvas.SetTop(box,en.Start.TimeOfDay.TotalHours*Hour);board.Children.Add(box);entryBoxes[en]=box;ToolTipService.SetShowDuration(box,60000);
  Point origin=new Point();bool moved=false,copy=false;int mode=0;List<Entry> originals=null,desired=null;var ghosts=new List<Border>();
  void ClearGhosts(){foreach(var ghost in ghosts)board.Children.Remove(ghost);ghosts.Clear();if(originals!=null)foreach(var row in originals)if(entryBoxes.TryGetValue(row,out var visual))visual.Opacity=1;}
  box.MouseLeftButtonDown+=(s,e)=>{
   e.Handled=true;origin=e.GetPosition(board);mode=HitMode(e.GetPosition(box).Y,box.Height);dragActive=true;
   if(!SelectEntry(en,Keyboard.Modifiers)){dragActive=false;return;}board.Focus();
   if(e.ClickCount==2){dragActive=false;Edit(en);return;}
   if(en.ReadOnlyReason!=null||!CanEdit(en)){dragActive=false;return;}
   copy=Keyboard.Modifiers.HasFlag(ModifierKeys.Control);moved=false;originals=null;dragActive=box.CaptureMouse();
  };
  box.MouseMove+=(s,e)=>{
   box.Cursor=HitMode(e.GetPosition(box).Y,box.Height)!=0?Cursors.SizeNS:Cursors.SizeAll;
   if(!box.IsMouseCaptured||e.LeftButton!=MouseButtonState.Pressed)return;var p=e.GetPosition(board);if((p-origin).Length<4&&!moved)return;
   if(!moved){selectedKeys.Add(EditingModel.Key(en));originals=SelectedEntries();if(originals.Count==0)originals=new List<Entry>{en};if(originals.Any(r=>r.ReadOnlyReason!=null)){dragActive=false;box.ReleaseMouseCapture();return;}moved=true;RefreshSelection();}
   int delta=RoundDelta((p.Y-origin.Y)/Hour*60);int dayDelta=Math.Clamp((int)((p.X-Gutter)/DayWidth),0,DisplayDayCount-1)-day;
   desired=originals.Select(row=>{var d=row.Copy();if(mode==0)d.Start=row.Start.AddDays(dayDelta).AddMinutes(delta);else if(mode==1){int shift=Math.Clamp(delta,-(int)row.Start.TimeOfDay.TotalMinutes,row.Minutes-5);d.Start=row.Start.AddMinutes(shift);d.Minutes=row.Minutes-shift;}else d.Minutes=ResizeDuration((int)row.Start.TimeOfDay.TotalMinutes,row.Minutes,delta);return d;}).ToList();
   ClearGhosts();
   for(int i=0;i<desired.Count;i++){var d=desired[i];var ghost=new Border {Width=Math.Max(12,DayWidth-8),Height=d.Minutes/60.0*Hour,Background=BrushOf(ColorFor(d.Project)),BorderBrush=BrushOf("#064FA3"),BorderThickness=new Thickness(2),Opacity=.75,IsHitTestVisible=false,ClipToBounds=true,Child=new TextBlock {Text=BlockCaption(d),TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=ReadableText(ColorFor(d.Project))}};Canvas.SetLeft(ghost,Gutter+(d.Start.Date-DisplayStart).Days*DayWidth+3);Canvas.SetTop(ghost,d.Start.TimeOfDay.TotalHours*Hour);Panel.SetZIndex(ghost,500);board.Children.Add(ghost);ghosts.Add(ghost);if(!copy&&entryBoxes.TryGetValue(originals[i],out var visual))visual.Opacity=.2;}
  };
  box.MouseLeftButtonUp+=async(s,e)=>{if(!box.IsMouseCaptured)return;e.Handled=true;dragActive=false;box.ReleaseMouseCapture();ClearGhosts();if(moved&&desired!=null){if(desired.Any(d=>d.Start<DisplayStart||d.Start.AddMinutes(d.Minutes)>DisplayStart.AddDays(DisplayDayCount)||d.Start.Date!=d.Start.AddMinutes(d.Minutes).AddTicks(-1).Date)){status.Text="表示範囲内・同日内に収めてください。";Render();return;}await ApplyBatchDrag(originals,desired,copy);}else {if(!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)&&!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)){selectedKeys.Clear();selectedKeys.Add(EditingModel.Key(en));selected=en;}RefreshSelection();}};
  box.LostMouseCapture+=(s,e)=>{if(dragActive){dragActive=false;ClearGhosts();Render();}};
  box.DragOver+=(s,e)=>{if(IsWorkDrop(e.Data)){e.Effects=DragDropEffects.Copy;e.Handled=true;}};
  box.Drop+=async(s,e)=>{if(!IsWorkDrop(e.Data))return;if(rangeStart.HasValue){DropWork(s,e);return;}e.Handled=true;if(!ApplyEditor()||!CanEdit(en))return;var work=ResolveDroppedWork(e.Data);if(work==null)return;var replacement=en.Copy();replacement.Project=work[0];replacement.Activity=work[1];var reusable=DroppedInput(e.Data);if(reusable!=null)replacement.Note=reusable.Note;if(!AssignIds(replacement))return;var before=en.Copy();RestoreEntry(en,replacement);await CommitEntry(en,before);};
  var menu=new ContextMenu();var edit=new MenuItem {Header="編集（Enter）"};edit.Click+=(s,e)=>Edit(en);menu.Items.Add(edit);var comment=new MenuItem {Header="コメント編集（F2）"};comment.Click+=(s,e)=>BeginInlineComment(en);menu.Items.Add(comment);var del=new MenuItem {Header="削除（Delete）"};del.Click+=async(s,e)=>await DeleteEntry(en);menu.Items.Add(del);box.ContextMenu=menu;
  var template=new MenuItem {Header="定型入力に登録…"};template.Click+=(s,e)=>{if(ApplyEditor())SaveTemplate(en);};menu.Items.Insert(2,template);
 }
 static Brush ReadableText(string hex){var c=(Color)ColorConverter.ConvertFromString(hex);return .2126*c.R+.7152*c.G+.0722*c.B<140?Brushes.White:Brushes.Black;}
}




