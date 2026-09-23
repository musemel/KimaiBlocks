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
public partial class Blocks {
 TextBox projectSearch = new TextBox();
 ListBox projectList = new ListBox();
 TreeView tree = new TreeView();
 bool rebuildingTree;
 void BuildSidebar(DockPanel root) {
  var left=new DockPanel {Width=280,Margin=new Thickness(12,0,12,0),Background=Brushes.White};
  DockPanel.SetDock(left,Dock.Left);root.Children.Add(left);
  var filters=new StackPanel {Margin=new Thickness(10)};DockPanel.SetDock(filters,Dock.Top);left.Children.Add(filters);
  filters.Children.Add(ButtonOf("表示プロジェクトを選択…",ShowProjectChoices));
  filters.Children.Add(Label("作業ツリー",17));
  search.Padding=new Thickness(7);search.Margin=new Thickness(3);search.ToolTip="プロジェクト・アクティビティ・フォルダを検索";
  filters.Children.Add(Label("作業項目・フォルダを検索",11));filters.Children.Add(search);var searchDelay=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(250)};searchDelay.Tick+=(s,e)=>{searchDelay.Stop();Populate();};search.TextChanged+=(s,e)=>{searchDelay.Stop();searchDelay.Start();};
  favorites.Content="★ お気に入りのみ";favorites.Margin=new Thickness(4,8,4,4);favorites.Click+=(s,e)=>Populate();filters.Children.Add(favorites);
  var hint=Label("右クリックでフォルダ追加・削除\nプロジェクトをフォルダへドラッグ",11);hint.Foreground=BrushOf("#63758A");filters.Children.Add(hint);
  VirtualizingPanel.SetIsVirtualizing(tree,true);VirtualizingPanel.SetVirtualizationMode(tree,VirtualizationMode.Recycling);ScrollViewer.SetCanContentScroll(tree,true);
  tree.BorderThickness=new Thickness(0);tree.Margin=new Thickness(4);tree.Background=Brushes.White;
  tree.ContextMenu=FolderMenu(null);left.Children.Add(tree);
 }
 void ShowProjectChoices() {
  var w=new Window {Title="表示プロジェクト",Owner=this,Width=540,Height=660,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var choices=new StackPanel {Margin=new Thickness(16)};w.Content=choices;
  projectSearch=new TextBox();projectList=new ListBox();
  choices.Children.Add(Label("表示プロジェクト",17));
  projectSearch.Padding=new Thickness(7);projectSearch.Margin=new Thickness(3);projectSearch.ToolTip="表示対象をプロジェクト名で検索";
  choices.Children.Add(Label("プロジェクト名で検索",11));choices.Children.Add(projectSearch);
  projectList.Height=460;projectList.Margin=new Thickness(3);projectList.BorderBrush=BrushOf("#E2E8F0");
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
  projectList.ItemsSource=Projects.Where(p=>Matches(p,projectSearch.Text)).Select(p=>new ProjectChoice(p,()=>!state.Hidden.Contains(p),enabled=>{
   if(enabled)state.Hidden.Remove(p);else if(!state.Hidden.Contains(p))state.Hidden.Add(p);
   Save();Populate();
  })).ToList();
 }
 static bool Matches(string value,string query) {return value.IndexOf(query.Trim(),StringComparison.CurrentCultureIgnoreCase)>=0;}
 string FolderOf(string project) {string f;return state.ProjectFolders.TryGetValue(project,out f)&&state.Folders.Contains(f)?f:null;}
 void Populate() {
  rebuildingTree=true;
  try {
   tree.Items.Clear();
   var root=Node("プロジェクト（フォルダ外）","root",true);root.ContextMenu=FolderMenu(null);MakeFolderTarget((FrameworkElement)root.Header,null);tree.Items.Add(root);
   foreach(var folder in state.Folders) {
    var node=Node("▣  "+folder,"folder:"+folder,false);node.ContextMenu=FolderMenu(folder);MakeFolderTarget((FrameworkElement)node.Header,folder);
    foreach(var p in Projects.Where(p=>!state.Hidden.Contains(p)&&FolderOf(p)==folder))AddProject(node,p,folder);
    if(node.Items.Count>0 || string.IsNullOrWhiteSpace(search.Text) || Matches(folder,search.Text))tree.Items.Add(node);
   }
   foreach(var p in Projects.Where(p=>!state.Hidden.Contains(p)&&FolderOf(p)==null))AddProject(root,p,"");
  } finally {rebuildingTree=false;}
 }
 TreeViewItem Node(string title,string key,bool root) {
  var header=Label(title,12);header.Padding=new Thickness(2,3,2,3);header.ToolTip=title;
  var node=new TreeViewItem {Header=header,IsExpanded=!state.Collapsed.Contains(key)||!string.IsNullOrWhiteSpace(search.Text)||favorites.IsChecked==true};
  node.Expanded+=(s,e)=>{if(e.OriginalSource!=node)return;if(!rebuildingTree&&string.IsNullOrWhiteSpace(search.Text)&&favorites.IsChecked!=true){state.Collapsed.Remove(key);Save();}};
  node.Collapsed+=(s,e)=>{if(e.OriginalSource!=node)return;if(!rebuildingTree&&string.IsNullOrWhiteSpace(search.Text)&&favorites.IsChecked!=true){if(!state.Collapsed.Contains(key))state.Collapsed.Add(key);Save();}};
  header.MouseRightButtonDown+=(s,e)=>node.IsSelected=true;
  return node;
 }
 void AddProject(TreeViewItem parent,string project,string folder) {
  var activities=ActivitiesFor(project).Where(a=>(favorites.IsChecked!=true||state.Favorites.Contains(project+"|"+a))&&Matches(project+" "+a+" "+folder,search.Text)).ToList();
  if(activities.Count==0)return;
  var node=Node("■  "+project,"project:"+project,false);var header=(TextBlock)node.Header;
  header.Foreground=BrushOf("#254D70");header.Background=BrushOf(ColorFor(project));
  DragSource(header,"project",project,DragDropEffects.Move);node.ContextMenu=FolderMenu(null);parent.Items.Add(node);
  bool filled=false;
  Action fill=()=>{if(filled)return;filled=true;node.Items.Clear();
  foreach(var activity in activities) {
   string key=project+"|"+activity;
   var row=new StackPanel {Orientation=Orientation.Horizontal};
   var name=Label(activity,12);name.MinWidth=90;name.Cursor=Cursors.Hand;name.ToolTip="カレンダーへドラッグして実績を作成";row.Children.Add(name);
   DragSource(name,"work",System.Text.Json.JsonSerializer.Serialize(new[]{project,activity}),DragDropEffects.Copy);
   var star=ButtonOf(state.Favorites.Contains(key)?"★":"☆",()=>{if(state.Favorites.Contains(key))state.Favorites.Remove(key);else state.Favorites.Add(key);Save();Populate();});
   star.Padding=new Thickness(5,0,5,0);star.Margin=new Thickness(2,0,2,0);star.ToolTip="お気に入りを切り替え";row.Children.Add(star);
   node.Items.Add(new TreeViewItem {Header=row,ContextMenu=FolderMenu(null)});
  }};
  if(node.IsExpanded)fill();else {node.Items.Add(new TreeViewItem());node.Expanded+=(s,e)=>{if(e.OriginalSource==node)fill();};}
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
   status.Text=format=="work"?"カレンダーにドロップして実績を作成":"フォルダにドロップしてプロジェクトを移動";
   DragDrop.DoDragDrop(element,new DataObject(format,value),effects);
  };
 }
 void MakeFolderTarget(FrameworkElement header,string folder) {
  header.AllowDrop=true;header.ToolTip=folder==null?"ここにプロジェクトをドロップするとフォルダ外へ移動":"ここにプロジェクトをドロップして格納";
  header.DragOver+=(s,e)=>{e.Effects=e.Data.GetDataPresent("project")?DragDropEffects.Move:DragDropEffects.None;e.Handled=true;};
  header.Drop+=(s,e)=>{e.Handled=true;if(!e.Data.GetDataPresent("project"))return;string p=e.Data.GetData("project") as string;if(!Projects.Contains(p))return;if(folder==null)state.ProjectFolders.Remove(p);else state.ProjectFolders[p]=folder;state.Collapsed.Remove(folder==null?"root":"folder:"+folder);Save();Populate();};
 }
 ContextMenu FolderMenu(string folder) {
  var menu=new ContextMenu();var add=new MenuItem {Header="フォルダを追加…"};add.Click+=(s,e)=>AddFolder();menu.Items.Add(add);
  if(folder!=null){var remove=new MenuItem {Header="このフォルダを削除（中のプロジェクトは残す）"};remove.Click+=(s,e)=>{state.RemoveFolder(folder);Save();Populate();};menu.Items.Add(remove);}
  return menu;
 }
 void AddFolder() {
  var w=new Window {Title="フォルダを追加",Owner=this,Width=340,Height=195,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var panel=new StackPanel {Margin=new Thickness(18)};w.Content=panel;panel.Children.Add(Label("フォルダ名",13));var input=new TextBox {Padding=new Thickness(6)};panel.Children.Add(input);
  var button=ButtonOf("追加",()=>{string name=input.Text.Trim();if(name.Length==0||state.Folders.Any(f=>string.Equals(f,name,StringComparison.CurrentCultureIgnoreCase))){MessageBox.Show(w,"空でない、重複しない名前を入力してください。");return;}state.Folders.Add(name);Save();Populate();w.Close();});button.IsDefault=true;panel.Children.Add(button);w.Loaded+=(s,e)=>input.Focus();w.ShowDialog();
 }
 static void SidebarTests() {
  var serializer=new DataContractJsonSerializer(typeof(State));
  using(var old=new MemoryStream(Encoding.UTF8.GetBytes("{\"Entries\":[],\"Hidden\":[],\"Favorites\":[]}"))) {
   var migrated=(State)serializer.ReadObject(old);if(migrated.Folders==null||migrated.ProjectFolders==null||migrated.Collapsed==null)throw new Exception("Old state migration failed");
  }
  var data=new State();data.Folders.Add("仕事");data.ProjectFolders["A"]="仕事";data.ProjectFolders["B"]="仕事";data.Hidden.Add("B");data.Collapsed.Add("folder:仕事");
  using(var stream=new MemoryStream()){serializer.WriteObject(stream,data);stream.Position=0;data=(State)serializer.ReadObject(stream);if(data.ProjectFolders["A"]!="仕事")throw new Exception("Folder persistence failed");}
  data.RemoveFolder("仕事");if(data.Folders.Count!=0||data.ProjectFolders.Count!=0||data.Collapsed.Count!=0||!data.Hidden.Contains("B"))throw new Exception("Folder removal failed");
  if(!Matches("Project A","project")||Matches("Project A","other"))throw new Exception("Search failed");
 }
}

