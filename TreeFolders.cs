using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Input;

[DataContract] public sealed class WorkLink {
 [DataMember] public string Project;
 [DataMember] public string Activity;
}
public partial class Blocks {
 void AddPinnedFavorites() {
  // Retain custom contents of older smart folders; empty smart folders are replaced by the built-in node.
  foreach(var folder in state.FavoriteFolders.ToArray()){if(state.FolderWorks.GetValueOrDefault(folder)?.Count>0||state.ProjectFolders.Values.Contains(folder)||state.FolderParents.Values.Contains(folder))state.FavoriteFolders.Remove(folder);else state.RemoveFolder(folder);}
  var pinned=Node("★ お気に入り","builtin:favorites",true);tree.Items.Add(pinned);
  foreach(var project in Projects)foreach(var activity in ActivitiesFor(project).Where(a=>state.Favorites.Contains(project+"|"+a)&&Matches(project+" "+a,search.Text)))AddWorkRow(pinned,project,activity,project+" · "+activity);
 }
 bool MoveFolderRelative(string folder,string target,bool after) {
  if(folder==target||!state.Folders.Contains(target)||!MoveFolder(folder,ParentFolder(target)))return false;
  state.Folders.Remove(folder);state.Folders.Insert(state.Folders.IndexOf(target)+(after?1:0),folder);return true;
 }
 string FolderLabel(string key)=>state.FolderLabels.GetValueOrDefault(key,key);
 string FolderPathLabel(string key){var parts=new List<string>();var seen=new HashSet<string>();for(string p=key;p!=null&&seen.Add(p);p=ParentFolder(p))parts.Insert(0,FolderLabel(p));return string.Join(" / ",parts);}
 string ParentFolder(string key)=>state.FolderParents.TryGetValue(key,out var parent)&&state.Folders.Contains(parent)?parent:null;
 void AddFolders(ItemsControl parent,string parentKey,HashSet<string> visited) {
  foreach(var folder in state.Folders.Where(f=>ParentFolder(f)==parentKey)) {
   if(!visited.Add(folder))continue;bool flat=state.FavoriteFolders.Contains(folder);
   var node=Node((flat?"★ ":"▣ ")+FolderLabel(folder),"folder:"+folder,false);node.ContextMenu=FolderMenu(folder);MakeFolderTarget((FrameworkElement)node.Header,folder);DragSource((FrameworkElement)node.Header,"folder",folder,DragDropEffects.Move);parent.Items.Add(node);
   AddFolders(node,folder,visited);
   if(flat) {
    foreach(var project in Projects.Where(p=>!state.Hidden.Contains(p)))foreach(var activity in ActivitiesFor(project).Where(a=>state.Favorites.Contains(project+"|"+a)&&Matches(project+" "+a,search.Text)))AddWorkRow(node,project,activity,project+" · "+activity);
   }else {
    foreach(var project in Projects.Where(p=>!state.Hidden.Contains(p)&&FolderOf(p)==folder))AddProject(node,project,FolderLabel(folder));
    if(state.FolderWorks.TryGetValue(folder,out var works))foreach(var work in works.Where(x=>Projects.Contains(x.Project)&&ActivitiesFor(x.Project).Contains(x.Activity)&&Matches(x.Project+" "+x.Activity,search.Text)&&(favorites.IsChecked!=true||state.Favorites.Contains(x.Project+"|"+x.Activity))))AddWorkRow(node,work.Project,work.Activity,work.Project+" · "+work.Activity);
   }
  }
 }
 bool MoveFolder(string folder,string parent) {
  if(!state.Folders.Contains(folder)||parent!=null&&!state.Folders.Contains(parent))return false;
  var visited=new HashSet<string>();for(string p=parent;p!=null;p=ParentFolder(p)){if(p==folder||!visited.Add(p))return false;}
  if(parent==null)state.FolderParents.Remove(folder);else state.FolderParents[folder]=parent;
  state.Folders.Remove(folder);state.Folders.Add(folder);return true;
 }
 void MoveFolderOrder(string folder,int direction) {
  var siblings=state.Folders.Where(f=>ParentFolder(f)==ParentFolder(folder)).ToList();int index=siblings.IndexOf(folder),other=index+direction;if(index<0||other<0||other>=siblings.Count)return;
  int a=state.Folders.IndexOf(folder),b=state.Folders.IndexOf(siblings[other]);(state.Folders[a],state.Folders[b])=(state.Folders[b],state.Folders[a]);Save();Populate();
 }
 void MakeFolderTarget(FrameworkElement header,string folder) {
  header.AllowDrop=true;header.ToolTip="フォルダをドラッグ：上端／下端にドロップで並べ替え、中央で中へ移動。個別作業も登録できます";
  bool Accept(IDataObject data)=>data.GetDataPresent("folder")||!state.FavoriteFolders.Contains(folder)&&(data.GetDataPresent("project")||folder!=null&&data.GetDataPresent("work"));
  header.DragOver+=(s,e)=>{e.Effects=Accept(e.Data)?e.Data.GetDataPresent("work")?DragDropEffects.Copy:DragDropEffects.Move:DragDropEffects.None;e.Handled=true;};
  header.Drop+=(s,e)=>{e.Handled=true;if(!Accept(e.Data))return;
   if(e.Data.GetDataPresent("folder")){string moved=e.Data.GetData("folder") as string;double y=e.GetPosition(header).Y;bool success=folder!=null&&(y<header.ActualHeight/3||y>header.ActualHeight*2/3)?MoveFolderRelative(moved,folder,y>header.ActualHeight*2/3):MoveFolder(moved,folder);if(!success){status.Text="自分自身や子フォルダの下には移動できません。";return;}}
   else if(e.Data.GetDataPresent("project")){string project=e.Data.GetData("project") as string;if(!Projects.Contains(project))return;if(folder==null)state.ProjectFolders.Remove(project);else state.ProjectFolders[project]=folder;}
   else {var pair=System.Text.Json.JsonSerializer.Deserialize<string[]>((string)e.Data.GetData("work"));if(!state.FolderWorks.TryGetValue(folder,out var works))state.FolderWorks[folder]=works=new List<WorkLink>();if(!works.Any(x=>x.Project==pair[0]&&x.Activity==pair[1]))works.Add(new WorkLink {Project=pair[0],Activity=pair[1]});}
   string key=folder==null?"root":"folder:"+folder;state.Collapsed.Remove(key);if(!state.ExpandedNodes.Contains(key))state.ExpandedNodes.Add(key);Save();Populate();
  };
 }
 ContextMenu FolderMenu(string folder) {
  var menu=new ContextMenu();void Item(string title,Action action){var item=new MenuItem {Header=title};item.Click+=(s,e)=>action();menu.Items.Add(item);}
  Item(folder==null?"フォルダを追加…":"サブフォルダを追加…",()=>EditFolderName(null,folder,false));

  if(folder!=null){Item("名前を変更…",()=>EditFolderName(folder,ParentFolder(folder),false));Item("上へ",()=>MoveFolderOrder(folder,-1));Item("下へ",()=>MoveFolderOrder(folder,1));Item("最上位へ移動",()=>{MoveFolder(folder,null);Save();Populate();});if(!state.FavoriteFolders.Contains(folder))Item("表示する作業を選択…",()=>ChooseFolderWorks(folder));Item("フォルダを削除（実績は削除しません）",()=>{state.RemoveFolder(folder);Save();Populate();});}
  return menu;
 }
 void AddFolder()=>EditFolderName(null,null,false);
 void EditFolderName(string key,string parent,bool flat) {
  var w=new Window {Title=key==null?"フォルダ追加":"フォルダ名変更",Owner=this,Width=390,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel {Margin=new Thickness(18)};w.Content=panel;panel.Children.Add(Label("名前",13));var input=new TextBox {Text=key==null?"":FolderLabel(key),Padding=new Thickness(6)};panel.Children.Add(input);
  panel.Children.Add(ButtonOf("保存",()=>{string label=input.Text.Trim();if(label.Length==0||state.Folders.Any(f=>f!=key&&ParentFolder(f)==parent&&FolderLabel(f)==label)){MessageBox.Show(w,"同じ階層で重複しない名前を入力してください。");return;}string id=key??Guid.NewGuid().ToString("N");if(key==null){state.Folders.Add(id);if(parent!=null)state.FolderParents[id]=parent;if(flat)state.FavoriteFolders.Add(id);}state.FolderLabels[id]=label;Save();Populate();w.Close();}));panel.Children.Add(ButtonOf("キャンセル",()=>w.Close()));w.Loaded+=(s,e)=>{input.Focus();input.SelectAll();};w.ShowDialog();
 }
 void ChooseFolderWorks(string folder) {
  var w=new Window {Title="フォルダに表示する作業",Owner=this,Width=620,Height=570,WindowStartupLocation=WindowStartupLocation.CenterOwner};var root=new DockPanel {Margin=new Thickness(16)};w.Content=root;
  var selected=(state.FolderWorks.GetValueOrDefault(folder)??new List<WorkLink>()).Select(x=>x.Project+"|"+x.Activity).ToHashSet();var choices=Projects.SelectMany(p=>ActivitiesFor(p).Select(a=>new WorkLink {Project=p,Activity=a})).ToList();
  var query=new TextBox {Margin=new Thickness(4),Padding=new Thickness(6)};DockPanel.SetDock(query,Dock.Top);root.Children.Add(query);var bottom=new WrapPanel();DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);var list=new ListBox();root.Children.Add(list);
  void Fill(){list.Items.Clear();foreach(var work in choices.Where(x=>Matches(x.Project+" "+x.Activity,query.Text))){string id=work.Project+"|"+work.Activity;var check=new CheckBox {Content=work.Project+" · "+work.Activity,IsChecked=selected.Contains(id),Margin=new Thickness(4)};check.Checked+=(s,e)=>selected.Add(id);check.Unchecked+=(s,e)=>selected.Remove(id);list.Items.Add(check);}}
  query.TextChanged+=(s,e)=>Fill();Fill();bottom.Children.Add(ButtonOf("保存",()=>{state.FolderWorks[folder]=choices.Where(x=>selected.Contains(x.Project+"|"+x.Activity)).ToList();Save();Populate();w.Close();}));bottom.Children.Add(ButtonOf("キャンセル",()=>w.Close()));w.ShowDialog();
 }
 void ApplyTreeGuides() {
  var style=(Style)XamlReader.Parse("""
  <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="TreeViewItem">
   <Setter Property="HorizontalAlignment" Value="Stretch"/><Setter Property="HorizontalContentAlignment" Value="Stretch"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TreeViewItem"><StackPanel>
    <DockPanel LastChildFill="True">
     <ToggleButton x:Name="Arrow" DockPanel.Dock="Left" Width="18" Height="22" HorizontalAlignment="Left" VerticalAlignment="Center" Background="Transparent" BorderThickness="0" Focusable="False" IsChecked="{Binding IsExpanded,RelativeSource={RelativeSource TemplatedParent}}"><TextBlock x:Name="Glyph" Text="›" FontSize="15" RenderTransformOrigin="0.5,0.5"/></ToggleButton>
     <Border x:Name="Head" BorderBrush="#E4EAF0" BorderThickness="0,0,0,1"><ContentPresenter ContentSource="Header" HorizontalAlignment="Left" VerticalAlignment="Center"/></Border>
    </DockPanel>
    <Border x:Name="Branch" Margin="8,0,0,0" BorderBrush="#CFD9E3" BorderThickness="1,0,0,0"><ItemsPresenter Margin="9,0,0,0"/></Border>
   </StackPanel><ControlTemplate.Triggers><Trigger Property="IsExpanded" Value="False"><Setter TargetName="Branch" Property="Visibility" Value="Collapsed"/></Trigger><Trigger Property="IsExpanded" Value="True"><Setter TargetName="Glyph" Property="RenderTransform"><Setter.Value><RotateTransform Angle="90"/></Setter.Value></Setter></Trigger><Trigger Property="HasItems" Value="False"><Setter TargetName="Arrow" Property="Visibility" Value="Hidden"/></Trigger><Trigger Property="IsSelected" Value="True"><Setter TargetName="Head" Property="Background" Value="#E4EFFA"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>  </Style>
  """);tree.ItemContainerStyle=style;tree.Resources[typeof(TreeViewItem)]=style;
 }
}





