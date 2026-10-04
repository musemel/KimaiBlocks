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
  var names=Projects.Concat(state.Entries.Select(e=>e.Project)).Where(p=>p!=null).Distinct().OrderBy(p=>p,StringComparer.Ordinal).ToArray();
  string signature=settings.ProjectPalette+":"+settings.AvoidColorCollisions+":"+string.Join("|",names)+string.Join("|",state.FixedColors.OrderBy(x=>x.Key).Select(x=>x.Key+":"+x.Value));if(signature==colorSignature)return;colorSignature=signature;projectColors.Clear();
  var used=new HashSet<string>(state.FixedColors.Values.Concat(state.AutoColors.Values),StringComparer.OrdinalIgnoreCase);
  foreach(var name in names) {
   if(state.FixedColors.TryGetValue(ProjectColorKey(name),out var fixedColor)){projectColors[name]=fixedColor;continue;}
   string color=StableColors.Color(name,settings.ProjectPalette);
   if(settings.ProjectPalette==0&&settings.AvoidColorCollisions){if(state.AutoColors.TryGetValue(name,out var prior)&&!state.FixedColors.Values.Contains(prior))color=prior;else {string original=color;for(int n=1;used.Contains(color)&&n<512;n++)color=StableColors.Nearby(original,n);state.AutoColors[name]=color;}used.Add(color);}
   projectColors[name]=color;
  }
 } internal static string AutoColor(int index) {
  double h=(index*137.507764)%360,s=.34+(index%3)*.08,v=.98-(index%2)*.04,c=v*s,x=c*(1-Math.Abs(h/60%2-1)),m=v-c;
  (double r,double g,double b)=h<60?(c,x,0d):h<120?(x,c,0d):h<180?(0d,c,x):h<240?(0d,x,c):h<300?(x,0d,c):(c,0d,x);
  return $"#{(int)Math.Round((r+m)*255):X2}{(int)Math.Round((g+m)*255):X2}{(int)Math.Round((b+m)*255):X2}";
 }
 string ColorFor(string project) {if(project==null)return "#DDE6EF";if(state.FixedColors.TryGetValue(ProjectColorKey(project),out var value))return value;return projectColors.TryGetValue(project,out var color)?color:StableColors.Color(project,settings.ProjectPalette);}
 void ResetAutomaticColors(){state.AutoColors.Clear();colorSignature="";RefreshColors();Save();Populate();Render();} void ColorDialog(string selectedProject=null) {
  var w=new Window {Title="プロジェクトの固定色",Owner=this,Width=600,Height=760,WindowStartupLocation=WindowStartupLocation.CenterOwner};var root=new DockPanel {Margin=new Thickness(16)};w.Content=root;Action refreshRows=()=>{};
  var automatic=new StackPanel {Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(automatic,Dock.Top);root.Children.Add(automatic);
  automatic.Children.Add(Label("自動色（プロジェクト名から決定）",15));
  var palette=new ComboBox {ItemsSource=new[]{"固定16色（標準）","固定256色","無制限"},SelectedIndex=settings.ProjectPalette==16?0:settings.ProjectPalette==256?1:2,Margin=new Thickness(4)};automatic.Children.Add(palette);
  var avoid=new CheckBox {Content="既存色と重なる場合は近縁色にする（無制限のみ）",IsChecked=settings.AvoidColorCollisions,IsEnabled=palette.SelectedIndex==2,Margin=new Thickness(4)};automatic.Children.Add(avoid);palette.SelectionChanged+=(s,e)=>avoid.IsEnabled=palette.SelectedIndex==2;
  automatic.Children.Add(ButtonOf("自動色の設定を保存して再設定",()=>{int old=settings.ProjectPalette;bool previous=settings.AvoidColorCollisions;try{settings.ProjectPalette=palette.SelectedIndex==0?16:palette.SelectedIndex==1?256:0;settings.AvoidColorCollisions=avoid.IsChecked==true;try{StoreSettings();}catch{settings.ProjectPalette=old;settings.AvoidColorCollisions=previous;throw;}ResetAutomaticColors();refreshRows();}catch(Exception ex){MessageBox.Show(w,SafeError(ex),"自動色の設定を保存できません");}}));
  automatic.Children.Add(Label("個別の固定色（自動色より優先）",15));
  var filter=new TextBox {Margin=new Thickness(4),Padding=new Thickness(5)};DockPanel.SetDock(filter,Dock.Top);root.Children.Add(filter);
  var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
  bottom.Children.Add(Label("固定色（#RRGGBB）。自動色はプロジェクト名から決まります。",12));var hex=new TextBox {Text="#B9DCF9",Padding=new Thickness(6)};bottom.Children.Add(hex);
  var preview=new Border {Height=25,Margin=new Thickness(4),Background=BrushOf(hex.Text)};bottom.Children.Add(preview);hex.TextChanged+=(s,e)=>{try{preview.Background=BrushOf(hex.Text);}catch{}};
  bottom.Children.Add(ButtonOf("色を選択…（Windows標準）",()=>{try{var selected=PickWindowsColor(w,hex.Text);if(selected!=null)hex.Text=selected;}catch(Exception ex){MessageBox.Show(w,ex.Message,"色を選択できません");}}));  var list=new ListBox {Margin=new Thickness(4)};root.Children.Add(list);
  void Fill(){string selected=list.SelectedValue as string;RefreshColors();list.Items.Clear();foreach(string project in Projects.Where(p=>Matches(p,filter.Text))){string color=ColorFor(project);bool fixedColor=state.FixedColors.ContainsKey(ProjectColorKey(project));var row=new DockPanel {Margin=new Thickness(2)};var swatch=new Border {Width=18,Height=18,Background=BrushOf(color),BorderBrush=Brushes.Gray,BorderThickness=new Thickness(1),Margin=new Thickness(2,0,8,0)};DockPanel.SetDock(swatch,Dock.Left);row.Children.Add(swatch);var mode=Label((fixedColor?"固定 ":"自動 ")+color,11);DockPanel.SetDock(mode,Dock.Right);row.Children.Add(mode);var title=Label(project,12);title.TextTrimming=TextTrimming.CharacterEllipsis;row.Children.Add(title);list.Items.Add(new ListBoxItem {Content=row,Tag=project,ToolTip=project+" · "+(fixedColor?"固定":"自動")+" "+color,HorizontalContentAlignment=HorizontalAlignment.Stretch});}list.SelectedValue=selected;}
  list.SelectedValuePath="Tag";refreshRows=Fill;filter.TextChanged+=(s,e)=>Fill();Fill();
  list.SelectionChanged+=(s,e)=>{if(list.SelectedValue is string p)hex.Text=ColorFor(p);};
  if(selectedProject!=null)list.SelectedValue=selectedProject;
  void Apply(bool fixedValue) {if(list.SelectedValue is not string project)return;string value=hex.Text.Trim();if(fixedValue&&(!System.Text.RegularExpressions.Regex.IsMatch(value,"^#[0-9a-fA-F]{6}$"))){MessageBox.Show(w,"#RRGGBB形式で入力してください。");return;}if(fixedValue)state.FixedColors[ProjectColorKey(project)]=value.ToUpperInvariant();else state.FixedColors.Remove(ProjectColorKey(project));colorSignature="";Save();RefreshColors();Populate();Render();Fill();}
  bottom.Children.Add(ButtonOf("指定色で固定",()=>Apply(true)));bottom.Children.Add(ButtonOf("自動割当へ戻す",()=>Apply(false)));bottom.Children.Add(ButtonOf("閉じる",()=>w.Close()));w.ShowDialog();
 }
}




