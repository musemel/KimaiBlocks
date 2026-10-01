using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

public partial class Blocks {
 StackPanel editorPanel;Entry editorEntry;bool editorDirty,buildingEditor;
 ComboBox editorProject,editorActivity;DatePicker editorDate;TextBox editorStart,editorMinutes,editorComment;TextBlock editorHeading;
 TextBox inlineComment;Entry inlineEntry;bool finishingInline;
 void BuildEditor(DockPanel parent) {
  editorPanel=new StackPanel {Margin=new Thickness(8)};var scroll=new ScrollViewer {Content=editorPanel,MaxHeight=340,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};DockPanel.SetDock(scroll,Dock.Bottom);parent.Children.Add(scroll);RenderEditor();
 }
 void RenderEditor() {
  if(editorPanel==null)return;
  if(editorDirty)return;
  buildingEditor=true;
  try {
   var rows=SelectedEntries();editorPanel.Children.Clear();editorEntry=rows.Count==1?rows[0]:null;
   editorHeading=Label(rows.Count==0?"選択中の実績なし":rows.Count+"件を選択 · "+Hours(rows.Sum(e=>e.Minutes)),14);editorHeading.FontWeight=FontWeights.SemiBold;editorPanel.Children.Add(editorHeading);
   if(rows.Count==0)return;
   var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(76)});grid.ColumnDefinitions.Add(new ColumnDefinition());editorPanel.Children.Add(grid);int row=0;
   void Field(string name,Control control){grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});var label=Label(name,11);Grid.SetRow(label,row);grid.Children.Add(label);Grid.SetRow(control,row++);Grid.SetColumn(control,1);control.Margin=new Thickness(2);grid.Children.Add(control);}
   if(editorEntry!=null) {
    editorProject=new ComboBox {ItemsSource=Projects,SelectedItem=editorEntry.Project};editorActivity=new ComboBox {ItemsSource=ActivitiesFor(editorEntry.Project),SelectedItem=editorEntry.Activity};editorDate=new DatePicker {SelectedDate=editorEntry.Start.Date};editorStart=new TextBox {Text=editorEntry.Start.ToString("HH:mm")};editorMinutes=new TextBox {Text=editorEntry.Minutes.ToString()};
    Field("プロジェクト",editorProject);Field("作業",editorActivity);Field("日付",editorDate);Field("開始",editorStart);Field("分",editorMinutes);
    editorProject.SelectionChanged+=(s,e)=>{if(buildingEditor)return;editorActivity.ItemsSource=ActivitiesFor(editorProject.SelectedItem as string);editorActivity.SelectedIndex=0;editorDirty=true;};editorActivity.SelectionChanged+=(s,e)=>{if(!buildingEditor)editorDirty=true;};editorDate.SelectedDateChanged+=(s,e)=>{if(!buildingEditor)editorDirty=true;};editorStart.TextChanged+=(s,e)=>{if(!buildingEditor)editorDirty=true;};editorMinutes.TextChanged+=(s,e)=>{if(!buildingEditor)editorDirty=true;};
   }
   editorComment=new TextBox {Text=rows.Count==1?rows[0].Note:"",Height=65,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,ToolTip=rows.Count>1?"入力して適用すると、選択実績のコメントを一括変更します。":"Ctrl+Enterで適用"};Field("コメント",editorComment);editorComment.TextChanged+=(s,e)=>{if(!buildingEditor)editorDirty=true;};editorComment.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Enter&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control)){e.Handled=true;ApplyEditor();}};
   bool editable=rows.All(e=>e.ReadOnlyReason==null);grid.IsEnabled=editable;
   var buttons=new WrapPanel();buttons.Children.Add(ButtonOf("適用",()=>ApplyEditor()));buttons.Children.Add(ButtonOf("入力を戻す",()=>{editorDirty=false;RenderEditor();}));editorPanel.Children.Add(buttons);buttons.IsEnabled=editable;
  }finally {buildingEditor=false;}
 }
 bool ApplyEditor() {
  if(inlineComment!=null&&!finishingInline)return FinishInline(true);
  if(!editorDirty)return true;var rows=SelectedEntries();if(rows.Count==0)return true;
  if(rows.Any(e=>!CanEdit(e)))return false;
  if(rows.Count>1){Remember();foreach(var en in rows){var old=en.Copy();en.Note=editorComment.Text;PendingQueue.Edit(state.Pending,en,old);}editorDirty=false;Save();Render();return true;}
  var entry=rows[0];
  if(editorProject.SelectedItem==null||editorActivity.SelectedItem==null||!editorDate.SelectedDate.HasValue||!TimeSpan.TryParse(editorStart.Text,out var time)||!int.TryParse(editorMinutes.Text,out var minutes)||!ValidTime(time,minutes)){MessageBox.Show(this,"プロジェクト・作業・日付・5分単位の時刻と時間を確認してください。");return false;}
  var desired=entry.Copy();desired.Project=(string)editorProject.SelectedItem;desired.Activity=(string)editorActivity.SelectedItem;desired.Start=editorDate.SelectedDate.Value.Date+time;desired.Minutes=minutes;desired.Note=editorComment.Text;if(!AssignIds(desired))return false;
  var before=entry.Copy();RestoreEntry(entry,desired);editorDirty=false;CommitEntry(entry,before).GetAwaiter().GetResult();return true;
 }
 void BeginInlineComment(Entry en) {
  if(!CanEdit(en)||!ApplyEditor()||!entryBoxes.TryGetValue(en,out var box))return;
  selectedKeys.Clear();selectedKeys.Add(EditingModel.Key(en));selected=en;RefreshSelection();
  inlineEntry=en;inlineComment=new TextBox {Text=en.Note,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=100,Width=Math.Max(180,Math.Min(420,DayWidth-6)),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(6),Background=Brushes.White,ToolTip="Enter: 確定 / Ctrl+Enter: 改行 / Esc: キャンセル"};
  Canvas.SetLeft(inlineComment,Canvas.GetLeft(box));Canvas.SetTop(inlineComment,Canvas.GetTop(box));Panel.SetZIndex(inlineComment,2000);board.Children.Add(inlineComment);inlineComment.TextChanged+=(s,e)=>editorDirty=true;
  inlineComment.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){e.Handled=true;FinishInline(false);}else if(e.Key==Key.Enter){e.Handled=true;if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)){int caret=inlineComment.SelectionStart;inlineComment.SelectedText=Environment.NewLine;inlineComment.Select(caret+Environment.NewLine.Length,0);}else FinishInline(true);}};
  var field=inlineComment;field.LostKeyboardFocus+=(s,e)=>{if(!finishingInline&&ReferenceEquals(inlineComment,field))FinishInline(true);};field.Focus();if(ReferenceEquals(inlineComment,field))field.SelectAll();
 }
 bool FinishInline(bool apply) {
  if(inlineComment==null)return true;finishingInline=true;
  try {var field=inlineComment;var en=inlineEntry;string text=field.Text;inlineComment=null;inlineEntry=null;editorDirty=false;board.Children.Remove(field);if(apply&&text!=en.Note){if(!CanEdit(en))return false;var before=en.Copy();en.Note=text;CommitEntry(en,before).GetAwaiter().GetResult();}else RenderEditor();return true;}
  finally {finishingInline=false;}
 }
}


