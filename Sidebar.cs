using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

public class ProjectChoice {
 public string Name {get;set;}
 public bool Enabled { get {return read();} set {write(value);} }
 readonly Func<bool> read;
 readonly Action<bool> write;
 public ProjectChoice(string name, Func<bool> read, Action<bool> write) {Name=name;this.read=read;this.write=write;}
}
public sealed class TreeRowWidth : IValueConverter {
 public object Convert(object value,Type target,object parameter,System.Globalization.CultureInfo culture)=>Math.Max(80,(value is double width?width:260)-System.Convert.ToDouble(parameter,culture));
 public object ConvertBack(object value,Type target,object parameter,System.Globalization.CultureInfo culture)=>Binding.DoNothing;
}
public partial class Blocks {
 TextBox projectSearch = new TextBox();
 ListBox projectList = new ListBox();
 TreeView tree = new TreeView();
 bool rebuildingTree;
 ActivityGrouping activityGrouping=new ActivityGrouping(ActivityGrouping.DefaultPattern);
 void BuildSidebar(DockPanel root) {
  var left=new DockPanel {Width=280,Margin=new Thickness(12,0,12,0),Background=Brushes.White};
  DockPanel.SetDock(left,Dock.Left);root.Children.Add(left);AddPanelResizer(root,left,Dock.Left);
  var filters=new StackPanel {Margin=new Thickness(10)};DockPanel.SetDock(filters,Dock.Top);left.Children.Add(filters);
  filters.Children.Add(ButtonOf("表示プロジェクトを選択…",ShowProjectChoices));
  filters.Children.Add(Label("作業ツリー",17));
  search.Padding=new Thickness(7);search.Margin=new Thickness(3);search.ToolTip="プロジェクト・アクティビティ・フォルダを検索";
  filters.Children.Add(Label("作業項目・フォルダを検索",11));filters.Children.Add(search);var searchDelay=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(250)};searchDelay.Tick+=(s,e)=>{searchDelay.Stop();Populate();};search.TextChanged+=(s,e)=>{searchDelay.Stop();searchDelay.Start();};
  favorites.Content="★ お気に入りのみ";favorites.Margin=new Thickness(4,8,4,4);favorites.Click+=(s,e)=>Populate();filters.Children.Add(favorites);
  var hint=Label("右クリックでフォルダ追加・削除\nプロジェクトをフォルダへドラッグ",11);hint.Foreground=BrushOf("#63758A");filters.Children.Add(hint);
  VirtualizingPanel.SetIsVirtualizing(tree,true);VirtualizingPanel.SetVirtualizationMode(tree,VirtualizationMode.Recycling);ScrollViewer.SetCanContentScroll(tree,true);
  tree.BorderThickness=new Thickness(0);tree.Margin=new Thickness(4);tree.Background=Brushes.White;
  ApplyTreeGuides();BuildReuseTabs(left,filters);
 }
 void ShowProjectChoices() {
  var w=new Window {Title="表示プロジェクト",Owner=this,Width=540,Height=660,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var choices=new StackPanel {Margin=new Thickness(16)};w.Content=choices;
  projectSearch=new TextBox();projectList=new ListBox();
  choices.Children.Add(Label("表示プロジェクト",17));
  projectSearch.Padding=new Thickness(7);projectSearch.Margin=new Thickness(3);projectSearch.ToolTip="表示対象をプロジェクト名で検索";
  choices.Children.Add(Label("プロジェクト名で検索",11));choices.Children.Add(projectSearch);
  choices.Children.Add(ButtonOf("すべてのチェックを外す",()=>{foreach(var p in Projects)if(!state.Hidden.Contains(p))state.Hidden.Add(p);Save();PopulateProjectList();Populate();Render();}));
  projectList.Height=420;projectList.Margin=new Thickness(3);projectList.BorderBrush=BrushOf("#E2E8F0");
  ScrollViewer.SetHorizontalScrollBarVisibility(projectList,ScrollBarVisibility.Disabled);
  VirtualizingPanel.SetIsVirtualizing(projectList,true);VirtualizingPanel.SetVirtualizationMode(projectList,VirtualizationMode.Recycling);
  var check=new FrameworkElementFactory(typeof(CheckBox));
  check.SetBinding(CheckBox.ContentProperty,new Binding("Name"));
  check.SetBinding(CheckBox.IsCheckedProperty,new Binding("Enabled") {Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});
  check.SetValue(CheckBox.MarginProperty,new Thickness(3,4,3,4));
  projectList.ItemTemplate=new DataTemplate {VisualTree=check};choices.Children.Add(projectList);
  projectSearch.TextChanged+=(s,e)=>PopulateProjectList();
  PopulateProjectList();choices.Children.Add(ButtonOf("閉じる",()=>w.Close()));w.ShowDialog();
 }
 void PopulateProjectList() {
  projectList.ItemsSource=Projects.Where(p=>Matches(p,projectSearch.Text)).OrderBy(p=>state.Hidden.Contains(p)).ThenBy(p=>p,StringComparer.CurrentCultureIgnoreCase).Select(p=>new ProjectChoice(p,()=>!state.Hidden.Contains(p),enabled=>{
   if(enabled)state.Hidden.Remove(p);else if(!state.Hidden.Contains(p))state.Hidden.Add(p);
   Save();Populate();Render();Dispatcher.BeginInvoke(new Action(PopulateProjectList));
  })).ToList();
 }
 static bool Matches(string value,string query) {string Clean(string s)=>new string((s??" ").Normalize(NormalizationForm.FormKC).Where(c=>!char.IsWhiteSpace(c)).ToArray());return System.Globalization.CultureInfo.GetCultureInfo("ja-JP").CompareInfo.IndexOf(Clean(value),Clean(query),System.Globalization.CompareOptions.IgnoreCase|System.Globalization.CompareOptions.IgnoreKanaType|System.Globalization.CompareOptions.IgnoreWidth)>=0;}
 string FolderOf(string project) {string f;return state.ProjectFolders.TryGetValue(project,out f)&&state.Folders.Contains(f)?f:null;}
 void Populate() {
  try {activityGrouping=new ActivityGrouping(EffectiveActivityPattern);}catch(ArgumentException){activityGrouping=new ActivityGrouping("");status.Text="アクティビティの正規表現が不正です。設定を確認してください。";}
  RefreshColors();rebuildingTree=true;
  try {
   tree.Items.Clear();AddPinnedFavorites();
   var root=Node("未分類","root",true);root.ContextMenu=FolderMenu(null);MakeFolderTarget((FrameworkElement)root.Header,null);tree.Items.Add(root);
   AddFolders(tree,null,new HashSet<string>());
   foreach(var p in Projects.Where(p=>!state.Hidden.Contains(p)&&FolderOf(p)==null))AddProject(root,p,"");
  } finally {rebuildingTree=false;}
 }
 TreeViewItem Node(string title,string key,bool root) {
  var header=Label(title,12);header.Padding=new Thickness(2,3,2,3);header.ToolTip=title;
  var node=new TreeViewItem {Header=header,IsExpanded=(root?!state.Collapsed.Contains(key):state.ExpandedNodes.Contains(key))||!string.IsNullOrWhiteSpace(search.Text)};
  node.Expanded+=(s,e)=>{if(e.OriginalSource!=node)return;if(!rebuildingTree&&string.IsNullOrWhiteSpace(search.Text)){state.Collapsed.Remove(key);if(!state.ExpandedNodes.Contains(key))state.ExpandedNodes.Add(key);Save();}};
  node.Collapsed+=(s,e)=>{if(e.OriginalSource!=node)return;if(!rebuildingTree&&string.IsNullOrWhiteSpace(search.Text)){state.ExpandedNodes.Remove(key);if(!state.Collapsed.Contains(key))state.Collapsed.Add(key);Save();}};
  header.MouseRightButtonDown+=(s,e)=>node.IsSelected=true;
  node.ContextMenuOpening+=(s,e)=>{if(node.ContextMenu!=null)return;var source=e.OriginalSource as DependencyObject;while(source!=null&&source is not TreeViewItem)source=source is Visual?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);if(ReferenceEquals(source,node))e.Handled=true;};
  return node;
 }
 void AddProject(TreeViewItem parent,string project,string folder) {
  var activities=ActivitiesFor(project).Where(a=>(favorites.IsChecked!=true||state.Favorites.Contains(project+"|"+a))&&Matches(project+" "+a+" "+folder,search.Text)).ToList();
  if(activities.Count==0)return;
  var projectParts=EditingModel.Path(project);var projectParent=PathParent(parent,projectParts.Take(Math.Max(0,projectParts.Length-1)),"project-path:"+folder);
  var node=Node("■  "+(projectParts.LastOrDefault()??project),"project:"+project,false);var header=(TextBlock)node.Header;
  header.Foreground=ReadableText(ColorFor(project));header.Background=BrushOf(ColorFor(project));header.ToolTip=project;
  DragSource(header,"project",project,DragDropEffects.Move|DragDropEffects.Copy);node.ContextMenu=new ContextMenu();var colorItem=new MenuItem {Header="このプロジェクトの色…"};colorItem.Click+=(s,e)=>ChooseProjectColor(project);node.ContextMenu.Items.Insert(0,colorItem);var autoColorItem=new MenuItem {Header="色を自動割当に戻す"};autoColorItem.Click+=(s,e)=>SetProjectColor(project,null);node.ContextMenu.Items.Add(autoColorItem);projectParent.Items.Add(node);
  bool filled=false;
  Action fill=()=>{if(filled)return;filled=true;node.Items.Clear();
  foreach(var activity in activities) {
   var grouping=activityGrouping.Split(activity);var activityParent=grouping.Group==null?node:PathParent(node,new[]{grouping.Group},"activity-regex:"+project);
   if(grouping.Group!=null&&((FrameworkElement)activityParent.Header).Tag==null){((FrameworkElement)activityParent.Header).Tag=true;DragSource((FrameworkElement)activityParent.Header,"work-group",System.Text.Json.JsonSerializer.Serialize(new[]{project,grouping.Group}),DragDropEffects.Copy);}
   AddWorkRow(activityParent,project,activity,grouping.Name);
  }};
  if(node.IsExpanded)fill();else {node.Items.Add(new TreeViewItem());node.Expanded+=(s,e)=>{if(e.OriginalSource==node)fill();};}
 }
 void AddWorkRow(TreeViewItem activityParent,string project,string activity,string label=null) {
   string key=project+"|"+activity;
   var row=new Grid {MinHeight=24};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
   int depth=1;for(var ancestor=activityParent;ancestor!=null;ancestor=ancestor.Parent as TreeViewItem)depth++;
   var card=new Border {Child=row,Background=BrushOf("#F3F6FA"),BorderBrush=BrushOf("#CCD9E6"),BorderThickness=new Thickness(2,0,0,0),CornerRadius=new CornerRadius(3),Padding=new Thickness(4,0,2,0),Margin=new Thickness(0,1,0,1)};
   card.SetBinding(FrameworkElement.WidthProperty,new Binding("ActualWidth") {Source=tree,Converter=new TreeRowWidth(),ConverterParameter=depth*19+20});
   card.MouseEnter+=(s,e)=>card.Background=BrushOf("#E2EEFB");card.MouseLeave+=(s,e)=>card.Background=BrushOf("#F3F6FA");
   var name=Label(label??activity,13);name.TextWrapping=TextWrapping.NoWrap;name.TextTrimming=TextTrimming.CharacterEllipsis;name.Foreground=BrushOf("#20354B");name.Margin=new Thickness(4,2,5,2);name.Cursor=Cursors.Hand;name.ToolTip=activity+"\nカレンダーへドラッグして実績を作成。既存ブロックへドロップすると作業を置換";row.Children.Add(name);
   DragSource(name,"work",System.Text.Json.JsonSerializer.Serialize(new[]{project,activity}),DragDropEffects.Copy);
   var star=ButtonOf(state.Favorites.Contains(key)?"★":"☆",()=>{if(state.Favorites.Contains(key))state.Favorites.Remove(key);else state.Favorites.Add(key);Save();Populate();});
   star.Padding=new Thickness(3);star.Margin=new Thickness(0);star.Width=26;star.Height=22;star.FontSize=15;star.VerticalAlignment=VerticalAlignment.Center;star.Background=Brushes.Transparent;star.BorderThickness=new Thickness(0);star.Foreground=state.Favorites.Contains(key)?BrushOf("#936000"):BrushOf("#687C90");star.ToolTip="お気に入りを切り替え";Grid.SetColumn(star,1);row.Children.Add(star);
   var leaf=new TreeViewItem {Header=card};leaf.ContextMenuOpening+=(s,e)=>e.Handled=true;activityParent.Items.Add(leaf);
 }
 TreeViewItem PathParent(TreeViewItem parent,IEnumerable<string> parts,string prefix) {
  string path=prefix;foreach(var part in parts){path+="/"+part;var child=parent.Items.OfType<TreeViewItem>().FirstOrDefault(n=>n.Tag as string==path);if(child==null){child=Node(part,path,false);child.Tag=path;if(prefix.StartsWith("activity-regex:")){var label=(TextBlock)child.Header;label.FontWeight=FontWeights.SemiBold;label.Foreground=BrushOf("#38546F");label.Margin=new Thickness(4,8,4,3);}parent.Items.Add(child);}parent=child;}return parent;
 }
 void DragSource(FrameworkElement element,string format,string value,DragDropEffects effects) {
  Point start=new Point();bool pressed=false;
  element.PreviewMouseLeftButtonDown+=(s,e)=>{start=e.GetPosition(this);pressed=true;element.CaptureMouse();e.Handled=true;};
  element.PreviewMouseLeftButtonUp+=(s,e)=>{pressed=false;if(element.IsMouseCaptured)element.ReleaseMouseCapture();};
  element.LostMouseCapture+=(s,e)=>pressed=false;
  element.MouseMove+=(s,e)=>{
   if(!pressed||e.LeftButton!=MouseButtonState.Pressed)return;
   var delta=e.GetPosition(this)-start;
   if(Math.Abs(delta.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(delta.Y)<SystemParameters.MinimumVerticalDragDistance)return;
   pressed=false;element.ReleaseMouseCapture();
   status.Text=format=="project"?"フォルダへ移動、またはカレンダーでアクティビティを選んで配置":"カレンダーにドロップして実績を作成";
   DragDrop.DoDragDrop(element,new DataObject(format,value),effects);
  };
 }
 static void SidebarTests() {
  ActivityGrouping.Tests();ManagedSettings.Tests();StableColors.Tests();
  var serializer=new DataContractJsonSerializer(typeof(State));
  using(var old=new MemoryStream(Encoding.UTF8.GetBytes("{\"Entries\":[],\"Hidden\":[],\"Favorites\":[]}"))) {
   var migrated=(State)serializer.ReadObject(old);if(migrated.Folders==null||migrated.ProjectFolders==null||migrated.Collapsed==null)throw new Exception("Old state migration failed");
  }
  var data=new State();data.Folders.Add("仕事");data.ProjectFolders["A"]="仕事";data.ProjectFolders["B"]="仕事";data.Hidden.Add("B");data.Collapsed.Add("folder:仕事");
  using(var stream=new MemoryStream()){serializer.WriteObject(stream,data);stream.Position=0;data=(State)serializer.ReadObject(stream);if(data.ProjectFolders["A"]!="仕事")throw new Exception("Folder persistence failed");}
  data.RemoveFolder("仕事");if(data.Folders.Count!=0||data.ProjectFolders.Count!=0||data.Collapsed.Count!=0||!data.Hidden.Contains("B"))throw new Exception("Folder removal failed");
  if(!Matches("ガラス　テスト","ｶﾞﾗｽﾃｽﾄ")||!Matches("カタカナ","かたかな")||!Matches("Project A","projecta"))throw new Exception("Japanese search failed");
  if(!Matches("Project A","project")||Matches("Project A","other"))throw new Exception("Search failed");
 }
}











