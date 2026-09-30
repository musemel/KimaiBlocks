using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

public partial class Blocks {
 readonly HashSet<string> selectedKeys=new HashSet<string>();string selectionAnchor;
 readonly Stack<List<Entry>> undo=new Stack<List<Entry>>(),redo=new Stack<List<Entry>>();
 List<Entry> clipboardEntries=new List<Entry>();DateTime? pasteTime;
 void Remember(){if(undo.Count>=100){var keep=undo.Take(99).Reverse().ToList();undo.Clear();foreach(var item in keep)undo.Push(item);}undo.Push(EditingModel.Snapshot(state.Entries));redo.Clear();}
 void ClearHistory(){undo.Clear();redo.Clear();selectionAnchor=null;pasteTime=null;selectedKeys.Clear();selected=null;editorDirty=false;editorEntry=null;}
 bool IsSelected(Entry en)=>selectedKeys.Contains(EditingModel.Key(en));
 void RefreshSelection() {
  if(selected!=null)selected=state.Entries.FirstOrDefault(e=>EditingModel.Key(e)==EditingModel.Key(selected));
  selectedKeys.IntersectWith(state.Entries.Select(EditingModel.Key));
  foreach(var pair in entryBoxes){pair.Value.BorderBrush=IsSelected(pair.Key)?BrushOf("#064FA3"):Brushes.White;pair.Value.BorderThickness=new Thickness(1);pair.Value.Effect=IsSelected(pair.Key)?new System.Windows.Media.Effects.DropShadowEffect {Color=Color.FromRgb(0,90,210),BlurRadius=4,ShadowDepth=0,Opacity=1}:null;}
  RenderEditor();UpdateEditToolbar();
 }
 bool SelectEntry(Entry en,ModifierKeys modifiers) {
  if(!ApplyEditor())return false;string key=EditingModel.Key(en);var visible=VisibleEntries().OrderBy(e=>e.Start).ThenBy(e=>e.Project).ToList();
  if(modifiers.HasFlag(ModifierKeys.Shift)&&selectionAnchor!=null){int a=visible.FindIndex(e=>EditingModel.Key(e)==selectionAnchor),b=visible.FindIndex(e=>EditingModel.Key(e)==key);if(a>=0&&b>=0){if(!modifiers.HasFlag(ModifierKeys.Control))selectedKeys.Clear();foreach(var row in visible.Skip(Math.Min(a,b)).Take(Math.Abs(a-b)+1))selectedKeys.Add(EditingModel.Key(row));}}
  else if(modifiers.HasFlag(ModifierKeys.Control)){if(!selectedKeys.Add(key))selectedKeys.Remove(key);selectionAnchor=key;}
  else {if(!selectedKeys.Contains(key)){selectedKeys.Clear();selectedKeys.Add(key);}selectionAnchor=key;}
  selected=selectedKeys.Contains(key)?en:state.Entries.FirstOrDefault(e=>selectedKeys.Contains(EditingModel.Key(e)));statisticsDay=en.Start.Date;RenderStatistics(VisibleEntries());RefreshSelection();return true;
 }
 List<Entry> SelectedEntries()=>state.Entries.Where(e=>selectedKeys.Contains(EditingModel.Key(e))).ToList();
 void UndoEdit(bool forward=false) {
  if(!CanEdit()||dragActive)return;
  if(editorDirty){if(!ApplyEditor())return;}
  var source=forward?redo:undo;var dest=forward?undo:redo;if(source.Count==0)return;
  dest.Push(EditingModel.Snapshot(state.Entries));EditingModel.Restore(state,source.Pop());editorDirty=false;selectedKeys.Clear();selected=null;Save();Render();status.Text=forward?"やり直しました · 保存待ち":"元に戻しました · 保存待ち";
 }
 async Task DeleteSelection() {
  if(!ApplyEditor())return;var rows=SelectedEntries();if(rows.Count==0||rows.Any(e=>!CanEdit(e)))return;
  if(MessageBox.Show(this,rows.Count+"件の実績を削除しますか？","選択実績の削除",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
  Remember();foreach(var row in rows){PendingQueue.Delete(state.Pending,row);state.Entries.Remove(row);}selected=null;selectedKeys.Clear();Save();Render();await Task.CompletedTask;
 }
 void SetupKeys() {
  PreviewKeyDown+=async(s,e)=>{
   if(OwnedWindows.Count>0||dragActive)return;
   bool ctrl=Keyboard.Modifiers.HasFlag(ModifierKeys.Control),shift=Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
   bool typing=Keyboard.FocusedElement is TextBoxBase||Keyboard.FocusedElement is PasswordBox||Keyboard.FocusedElement is ComboBox;
   if(ctrl&&e.Key==Key.S){e.Handled=true;if(ApplyEditor())await FlushAsync(true);return;}
   if(typing)return;
   if(ctrl&&e.Key==Key.Z){e.Handled=true;UndoEdit(shift);}
   else if(ctrl&&e.Key==Key.Y){e.Handled=true;UndoEdit(true);}
   else if(ctrl&&e.Key==Key.A){e.Handled=true;if(!ApplyEditor())return;selectedKeys.Clear();foreach(var row in VisibleEntries().Where(r=>(r.Start.Date-week).Days<DisplayDayCount))selectedKeys.Add(EditingModel.Key(row));selected=SelectedEntries().FirstOrDefault();RefreshSelection();}
   else if(ctrl&&e.Key==Key.C){e.Handled=true;CopySelection();}
   else if(ctrl&&e.Key==Key.V){e.Handled=true;await PasteSelection();}
   else if(e.Key==Key.Delete){e.Handled=true;await DeleteSelection();}
   else if(e.Key==Key.F2&&selected!=null){e.Handled=true;BeginInlineComment(selected);}
   else if(e.Key==Key.Enter&&selected!=null){e.Handled=true;Edit(selected);}
   else if(e.Key==Key.Escape){e.Handled=true;selectedKeys.Clear();selected=null;RefreshSelection();}
   else if(ctrl&&(e.Key==Key.Add||e.Key==Key.OemPlus)){e.Handled=true;SetZoom(Hour/144*100+25);}
   else if(ctrl&&(e.Key==Key.Subtract||e.Key==Key.OemMinus)){e.Handled=true;SetZoom(Hour/144*100-25);}
  };
  calendarScroll.PreviewMouseWheel+=(s,e)=>{if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)){e.Handled=true;SetZoom(Hour/144*100+(e.Delta>0?25:-25),e.GetPosition(calendarScroll).Y);}};
 }
 void SetZoom(double percent,double anchor=0) {if(dragActive)return;double minute=(calendarScroll.VerticalOffset+anchor)/Hour;Hour=144*Math.Clamp(percent,50,400)/100;board.Height=24*Hour;Render();calendarScroll.ScrollToVerticalOffset(Math.Max(0,minute*Hour-anchor));settings.ZoomPercent=(int)Math.Round(Hour/144*100);SaveViewPreferences();status.Text="表示倍率 "+settings.ZoomPercent+"%";}
 void AddPanelResizer(DockPanel root,FrameworkElement panel,Dock side) {
  var grip=new Thumb {Width=6,Background=BrushOf("#DCE3EA"),Cursor=Cursors.SizeWE};DockPanel.SetDock(grip,side);root.Children.Add(grip);
  grip.DragDelta+=(s,e)=>panel.Width=Math.Clamp(panel.Width+(side==Dock.Left?e.HorizontalChange:-e.HorizontalChange),200,Math.Min(600,Math.Max(220,ActualWidth*.38)));
  grip.DragCompleted+=(s,e)=>{if(side==Dock.Left)settings.LeftPanelWidth=panel.Width;else settings.RightPanelWidth=panel.Width;SaveViewPreferences();};
  if(side==Dock.Right)rightGrip=grip;else leftPanel=panel;
 }
 Thumb rightGrip;FrameworkElement leftPanel;
 void SaveViewPreferences(){if(demoMode||settings.Accounts.Count==0)return;try{StoreSettings();}catch(Exception ex){status.Text="表示設定の保存失敗: "+SafeError(ex);}}
 void RestoreViewPreferences(){Hour=144*Math.Clamp(settings.ZoomPercent,50,400)/100.0;board.Height=24*Hour;if(leftPanel!=null)leftPanel.Width=Math.Clamp(settings.LeftPanelWidth,200,600);if(statisticsPanel!=null)statisticsPanel.Width=Math.Clamp(settings.RightPanelWidth,200,600);}

 async Task ApplyBatchDrag(List<Entry> originals,List<Entry> desired,bool copy) {
  if(!CanEdit()||originals.Any(e=>!copy&&!CanEdit(e)))return;
  var exclude=originals.Select(EditingModel.Key).ToHashSet();var occupied=state.Entries.Where(e=>copy||!exclude.Contains(EditingModel.Key(e))).ToList();var planned=new List<List<Entry>>();
  foreach(var candidate in desired) {
   if(candidate.Start<week||candidate.Start.AddMinutes(candidate.Minutes)>week.AddDays(DisplayDayCount)||candidate.Start.Date!=candidate.Start.AddMinutes(candidate.Minutes).AddTicks(-1).Date){status.Text="表示範囲内・同日内に収めてください。";Render();return;}
   var parts=EditingModel.Plan(candidate,occupied,Rules);if(parts.Count==0){status.Text="入力可能な空き時間がありません。変更しませんでした。";Render();return;}planned.Add(parts);occupied.AddRange(parts);
  }
  Remember();selectedKeys.Clear();
  for(int i=0;i<planned.Count;i++) {
   int begin=0;if(!copy){var old=originals[i];var before=old.Copy();RestoreEntry(old,planned[i][0]);PendingQueue.Edit(state.Pending,old,before);selectedKeys.Add(EditingModel.Key(old));begin=1;}
   foreach(var part in planned[i].Skip(begin)){state.Entries.Add(part);PendingQueue.Edit(state.Pending,part,null);selectedKeys.Add(EditingModel.Key(part));}
  }
  selected=SelectedEntries().FirstOrDefault();if(selected!=null)statisticsDay=selected.Start.Date;Save();Render();status.Text="選択実績を"+(copy?"コピー":"変更")+"しました · 重複・入力禁止時間を除外 · 保存待ち";await Task.CompletedTask;
 }
}

