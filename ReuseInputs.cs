using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

[DataContract] public sealed class SavedInput {
 [DataMember] public string Name="";
 [DataMember] public Entry Value;
}
public partial class Blocks {
 TabControl workTabs;
 readonly ListBox historyInputs=new ListBox(),templateInputs=new ListBox();

 void BuildReuseTabs(DockPanel left,StackPanel filters) {
  left.Children.Remove(filters);
  var work=new DockPanel();DockPanel.SetDock(filters,Dock.Top);work.Children.Add(filters);work.Children.Add(tree);
  workTabs=new TabControl {Margin=new Thickness(2),BorderThickness=new Thickness(0)};
  workTabs.Items.Add(new TabItem {Header="作業",Content=work});
  DockPanel Page(ListBox list,bool templates) {
   var panel=new DockPanel();var tools=new StackPanel {Margin=new Thickness(6)};DockPanel.SetDock(tools,Dock.Top);panel.Children.Add(tools);
   var hint=Label(templates?"繰り返す入力を登録して再利用\nカレンダーへドラッグして配置":"最近使った入力（最大100件）\nコメントと長さを含めてドラッグ",11);hint.TextWrapping=TextWrapping.Wrap;tools.Children.Add(hint);
   var query=new TextBox {Margin=new Thickness(4),Padding=new Thickness(6),ToolTip="プロジェクト・作業・コメント・名前で検索"};tools.Children.Add(query);query.TextChanged+=(s,e)=>RefreshReuseList(list,templates,query.Text);list.Tag=query;
   if(templates)tools.Children.Add(ButtonOf("選択中の実績を登録…",()=>{if(ApplyEditor()&&SelectedEntries().Count==1)SaveTemplate(SelectedEntries()[0]);else status.Text="登録する実績を1件選択してください。";}));
   else tools.Children.Add(ButtonOf("履歴を消去",()=>{if(MessageBox.Show(this,"入力履歴を消去しますか？実績と定型入力は残ります。","履歴の消去",MessageBoxButton.YesNo)==MessageBoxResult.Yes){state.InputHistory.Clear();Save();RefreshReuse();}}));
   list.BorderThickness=new Thickness(0);list.HorizontalContentAlignment=HorizontalAlignment.Stretch;ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);panel.Children.Add(list);return panel;
  }
  workTabs.Items.Add(new TabItem {Header="履歴",Content=Page(historyInputs,false)});
  workTabs.Items.Add(new TabItem {Header="定型",Content=Page(templateInputs,true)});
  workTabs.SelectionChanged+=(s,e)=>{if(e.Source==workTabs)RefreshReuse();};left.Children.Add(workTabs);
 }
 static bool SameInput(Entry a,Entry b)=>a.Project==b.Project&&a.Activity==b.Activity&&a.Note==b.Note&&a.Minutes==b.Minutes;
 void RecordInput(Entry entry) {
  if(entry==null||entry.Minutes<5)return;
  var value=BlockOperations.CopyAsNew(entry);value.Start=new DateTime(2000,1,1);value.Minutes=Math.Clamp(RoundDelta(value.Minutes),5,1440);
  state.InputHistory.RemoveAll(e=>SameInput(e,value));state.InputHistory.Insert(0,value);
  if(state.InputHistory.Count>100)state.InputHistory.RemoveRange(100,state.InputHistory.Count-100);
 }
 void SeedInputHistory(IEnumerable<Entry> entries) {
  // Downloaded records fill unused history slots without reordering recent local input.
  foreach(var entry in entries.OrderByDescending(e=>e.Start)) {
   if(state.InputHistory.Count>=100)break;if(entry.Minutes<5)continue;
   var value=BlockOperations.CopyAsNew(entry);value.Start=new DateTime(2000,1,1);value.Minutes=Math.Clamp(RoundDelta(value.Minutes),5,1440);
   if(!state.InputHistory.Any(e=>SameInput(e,value)))state.InputHistory.Add(value);
  }
 }
 void RefreshReuse(){if(workTabs==null)return;RefreshReuseList(historyInputs,false,(historyInputs.Tag as TextBox)?.Text??"");RefreshReuseList(templateInputs,true,(templateInputs.Tag as TextBox)?.Text??"");}
 bool InputAvailable(Entry value) {if(service!=null){if(value.ProjectId>0)value.Project=service.ProjectName(value.ProjectId);if(value.ActivityId>0)value.Activity=service.ActivityName(value.ActivityId);}return Projects.Contains(value.Project)&&ActivitiesFor(value.Project).Contains(value.Activity);}
 void RefreshReuseList(ListBox list,bool templates,string query) {
  list.Items.Clear();var items=templates?state.InputTemplates:state.InputHistory.Select(e=>new SavedInput {Value=e}).ToList();
  foreach(var item in items.Where(t=>t.Value!=null&&InputAvailable(t.Value)&&Matches(t.Name+" "+t.Value.Project+" "+t.Value.Activity+" "+t.Value.Note,query))) {
   var value=item.Value;var panel=new StackPanel();var title=Label(string.IsNullOrEmpty(item.Name)?value.Project:item.Name,12);title.FontWeight=FontWeights.SemiBold;title.TextTrimming=TextTrimming.CharacterEllipsis;panel.Children.Add(title);
   var note=Label((string.IsNullOrWhiteSpace(value.Note)?"（コメントなし）":value.Note.Replace("\r","").Replace("\n"," "))+"\n"+value.Activity+" · "+Hours(value.Minutes),11);note.TextTrimming=TextTrimming.CharacterEllipsis;panel.Children.Add(note);
   var card=new Border {Child=panel,Background=BrushOf("#F3F6FA"),BorderBrush=BrushOf(ColorFor(value.Project)),BorderThickness=new Thickness(3,0,0,0),Margin=new Thickness(2),Cursor=Cursors.Hand,ToolTip=value.Project+"\n"+value.Activity+"\n"+value.Note+"\n"+Hours(value.Minutes)+" · ドラッグして配置"};
   DragSource(card,"saved-input",System.Text.Json.JsonSerializer.Serialize(value,new System.Text.Json.JsonSerializerOptions {IncludeFields=true}),DragDropEffects.Copy);
   var menu=new ContextMenu();var save=new MenuItem {Header=templates?"名前を変更…":"定型入力に登録…"};save.Click+=(s,e)=>SaveTemplate(value,templates?item:null);menu.Items.Add(save);
   var remove=new MenuItem {Header=templates?"定型入力を削除":"履歴から削除"};remove.Click+=(s,e)=>{if(templates)state.InputTemplates.Remove(item);else state.InputHistory.Remove(value);Save();RefreshReuse();};menu.Items.Add(remove);card.ContextMenu=menu;list.Items.Add(new ListBoxItem {Content=card});
  }
  if(list.Items.Count==0)list.Items.Add(new ListBoxItem {Content="該当する入力はありません",IsEnabled=false});
 }
 void SaveTemplate(Entry source,SavedInput existing=null) {
  var w=new Window {Title="定型入力を登録",Owner=this,Width=440,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel {Margin=new Thickness(18)};w.Content=panel;
  panel.Children.Add(Label("名前（コメント・作業・長さを保存）",13));var name=new TextBox {Text=existing?.Name??(string.IsNullOrWhiteSpace(source.Note)?source.Activity:source.Note.Split('\n')[0]),Margin=new Thickness(4),Padding=new Thickness(6)};panel.Children.Add(name);
  panel.Children.Add(ButtonOf("登録",()=>{if(string.IsNullOrWhiteSpace(name.Text))return;var item=existing??new SavedInput();item.Name=name.Text.Trim();item.Value=BlockOperations.CopyAsNew(source);item.Value.Start=new DateTime(2000,1,1);item.Value.Minutes=Math.Clamp(RoundDelta(source.Minutes),5,1440);if(existing==null)state.InputTemplates.Add(item);Save();RefreshReuse();w.Close();}));panel.Children.Add(ButtonOf("キャンセル",()=>w.Close()));w.Loaded+=(s,e)=>{name.Focus();name.SelectAll();};w.ShowDialog();
 }
 static Entry DroppedInput(IDataObject data)=>data.GetDataPresent("saved-input")?System.Text.Json.JsonSerializer.Deserialize<Entry>((string)data.GetData("saved-input"),new System.Text.Json.JsonSerializerOptions {IncludeFields=true}):null;
}
