using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

public partial class Blocks {
 Button undoButton,redoButton,saveButton,copyButton,pasteButton,deleteButton;
 void BuildEditToolbar(DockPanel root) {
  var bar=new StackPanel {Orientation=Orientation.Horizontal,Background=BrushOf("#F2F5F8"),Margin=new Thickness(0,0,0,4)};
  DockPanel.SetDock(bar,Dock.Top);root.Children.Add(bar);
  Button Icon(string name,string shortcut,string geometry,Action action) {
   var icon=new System.Windows.Shapes.Path {Data=Geometry.Parse(geometry),Stroke=BrushOf("#29445E"),StrokeThickness=1.6,Width=18,Height=18,Stretch=Stretch.Uniform,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};
   var button=new Button {Content=icon,Width=36,Height=32,Margin=new Thickness(3,3,0,3),Padding=new Thickness(7),Focusable=false,ToolTip=name+" ("+shortcut+")"};
   AutomationProperties.SetName(button,name);AutomationProperties.SetHelpText(button,shortcut);
   button.Click+=(s,e)=>action();button.IsEnabledChanged+=(s,e)=>icon.Opacity=button.IsEnabled?1:.3;bar.Children.Add(button);return button;
  }
  void Separator()=>bar.Children.Add(new Border {Width=1,Margin=new Thickness(8,7,5,7),Background=BrushOf("#CDD7E0")});
  undoButton=Icon("元に戻す","Ctrl+Z","M 8,2 L 2,7 L 8,12 M 2,7 L 12,7 C 21,7 21,19 12,19",()=>UndoEdit());
  redoButton=Icon("やり直す","Ctrl+Y / Ctrl+Shift+Z","M 12,2 L 18,7 L 12,12 M 18,7 L 8,7 C -1,7 -1,19 8,19",()=>UndoEdit(true));
  Separator();
  saveButton=Icon("保存","Ctrl+S","M 2,2 L 16,2 L 20,6 L 20,20 L 2,20 Z M 6,2 L 6,8 L 15,8 L 15,2 M 6,20 L 6,13 L 16,13 L 16,20",async()=>{if(ApplyEditor())await FlushAsync(true);});
  Separator();
  copyButton=Icon("コピー","Ctrl+C","M 7,7 L 20,7 L 20,20 L 7,20 Z M 3,15 L 1,15 L 1,1 L 15,1 L 15,3",CopySelection);
  pasteButton=Icon("貼り付け","Ctrl+V","M 6,4 L 2,4 L 2,21 L 19,21 L 19,4 L 15,4 M 6,2 L 15,2 L 15,7 L 6,7 Z M 6,12 L 15,12 M 6,16 L 15,16",async()=>await PasteSelection());
  deleteButton=Icon("削除","Delete","M 2,5 L 20,5 M 7,5 L 7,1 L 15,1 L 15,5 M 4,5 L 5,21 L 17,21 L 18,5 M 9,9 L 9,17 M 13,9 L 13,17",async()=>await DeleteSelection());
  AddViewTools(bar);
  CommandManager.RequerySuggested+=ToolbarRequery;
  Closed+=(s,e)=>CommandManager.RequerySuggested-=ToolbarRequery;
  UpdateEditToolbar();
 }
 void ToolbarRequery(object sender,EventArgs e)=>UpdateEditToolbar();
 void UpdateEditToolbar() {
  UpdateViewTools();if(undoButton==null)return;
  bool ready=!communicating&&!dragActive&&(demoMode||service!=null&&!needsRefresh&&!state.Pending.Any(p=>p.Attempted));
  var rows=SelectedEntries();bool writable=ready&&rows.All(e=>string.IsNullOrEmpty(e.ReadOnlyReason));
  undoButton.IsEnabled=ready&&(undo.Count>0||editorDirty);redoButton.IsEnabled=ready&&redo.Count>0&&!editorDirty;
  saveButton.IsEnabled=ready;copyButton.IsEnabled=!communicating&&!dragActive&&rows.Count>0;
  pasteButton.IsEnabled=ready&&clipboardEntries.Count>0;deleteButton.IsEnabled=writable&&rows.Count>0;
 }
 void CopySelection() {
  if(!ApplyEditor())return;clipboardEntries=EditingModel.Snapshot(SelectedEntries());UpdateEditToolbar();
  status.Text=clipboardEntries.Count+"件をコピーしました。カレンダーをクリックして貼り付け、またはCtrl+Vで配置します。";
 }
 async Task PasteSelection() {
  if(!CanEdit()||!ApplyEditor()||clipboardEntries.Count==0)return;
  var start=clipboardEntries.Min(r=>r.Start);var target=pasteTime??(selected!=null?selected.Start.AddMinutes(selected.Minutes):DisplayStart.AddHours(9));
  await ApplyBatchDrag(clipboardEntries,clipboardEntries.Select(r=>{var copy=r.Copy();copy.Start=target+(r.Start-start);return copy;}).ToList(),true);
 }
}


