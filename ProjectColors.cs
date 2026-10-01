using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

public partial class Blocks {
 readonly Dictionary<string,string> projectColors=new Dictionary<string,string>();string colorSignature="";
 static string ProjectColorKey(string project){int index=(project??"").LastIndexOf(" [#",StringComparison.Ordinal);return index>=0?project.Substring(index):project??"";}
 void RefreshColors() {
  var names=Projects.Where(p=>!state.Hidden.Contains(p)).Concat(state.Entries.Select(e=>e.Project)).Where(p=>p!=null).Distinct().OrderBy(p=>p,StringComparer.Ordinal).ToArray();
  string signature=string.Join("|",names)+string.Join("|",state.FixedColors.OrderBy(x=>x.Key).Select(x=>x.Key+":"+x.Value));if(signature==colorSignature)return;colorSignature=signature;projectColors.Clear();
  var used=new HashSet<string>(state.FixedColors.Values,StringComparer.OrdinalIgnoreCase);int index=0;
  foreach(var name in names) {
   if(state.FixedColors.TryGetValue(ProjectColorKey(name),out var fixedColor)){projectColors[name]=fixedColor;continue;}
   string color;do{color=AutoColor(index++);}while(!used.Add(color));projectColors[name]=color;
  }
 }
 internal static string AutoColor(int index) {
  double h=(index*137.507764)%360,s=.34+(index%3)*.08,v=.98-(index%2)*.04,c=v*s,x=c*(1-Math.Abs(h/60%2-1)),m=v-c;
  (double r,double g,double b)=h<60?(c,x,0d):h<120?(x,c,0d):h<180?(0d,c,x):h<240?(0d,x,c):h<300?(x,0d,c):(c,0d,x);
  return $"#{(int)Math.Round((r+m)*255):X2}{(int)Math.Round((g+m)*255):X2}{(int)Math.Round((b+m)*255):X2}";
 }
 string ColorFor(string project) {if(project==null)return "#DDE6EF";if(state.FixedColors.TryGetValue(ProjectColorKey(project),out var fixedColor))return fixedColor;if(!projectColors.TryGetValue(project,out var color)){int index=projectColors.Count;do{color=AutoColor(index++);}while(projectColors.Values.Contains(color)||state.FixedColors.Values.Contains(color));projectColors[project]=color;}return color;}
 void ColorDialog(string selectedProject=null) {
  var w=new Window {Title="プロジェクトの固定色",Owner=this,Width=540,Height=570,WindowStartupLocation=WindowStartupLocation.CenterOwner};var root=new DockPanel {Margin=new Thickness(16)};w.Content=root;
  var filter=new TextBox {Margin=new Thickness(4),Padding=new Thickness(5)};DockPanel.SetDock(filter,Dock.Top);root.Children.Add(filter);
  var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
  bottom.Children.Add(Label("固定色（#RRGGBB）。自動に戻すと表示対象の色を再割当します。",12));var hex=new TextBox {Text="#B9DCF9",Padding=new Thickness(6)};bottom.Children.Add(hex);
  var preview=new Border {Height=25,Margin=new Thickness(4),Background=BrushOf(hex.Text)};bottom.Children.Add(preview);hex.TextChanged+=(s,e)=>{try{preview.Background=BrushOf(hex.Text);}catch{}};
  bottom.Children.Add(ButtonOf("色を選択…（Windows標準）",()=>{try{var selected=PickWindowsColor(w,hex.Text);if(selected!=null)hex.Text=selected;}catch(Exception ex){MessageBox.Show(w,ex.Message,"色を選択できません");}}));  var list=new ListBox {Margin=new Thickness(4)};root.Children.Add(list);
  void Fill()=>list.ItemsSource=Projects.Where(p=>Matches(p,filter.Text)).ToArray();filter.TextChanged+=(s,e)=>Fill();Fill();
  list.SelectionChanged+=(s,e)=>{if(list.SelectedItem is string p)hex.Text=ColorFor(p);};
  if(selectedProject!=null)list.SelectedItem=selectedProject;
  void Apply(bool fixedValue) {if(list.SelectedItem is not string project)return;string value=hex.Text.Trim();if(fixedValue&&(!System.Text.RegularExpressions.Regex.IsMatch(value,"^#[0-9a-fA-F]{6}$"))){MessageBox.Show(w,"#RRGGBB形式で入力してください。");return;}if(fixedValue)state.FixedColors[ProjectColorKey(project)]=value.ToUpperInvariant();else state.FixedColors.Remove(ProjectColorKey(project));colorSignature="";Save();RefreshColors();Populate();Render();}
  bottom.Children.Add(ButtonOf("指定色で固定",()=>Apply(true)));bottom.Children.Add(ButtonOf("自動割当へ戻す",()=>Apply(false)));bottom.Children.Add(ButtonOf("閉じる",()=>w.Close()));w.ShowDialog();
 }
}

