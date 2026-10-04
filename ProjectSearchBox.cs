using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

public sealed class ProjectSearchBox : ComboBox {
 readonly string[] projects;readonly Func<string,string,bool> matches;
 TextBox input;bool updating;
 public ProjectSearchBox(string[] projects,string selected,Func<string,string,bool> matches) {
  this.projects=projects;this.matches=matches;IsEditable=true;IsTextSearchEnabled=false;StaysOpenOnEdit=true;MaxDropDownHeight=320;
  ToolTip="プロジェクト名を入力して検索（かな・全半角・空白の違いを吸収）。候補を選択して確定。";
  ItemsSource=projects;SelectedItem=selected;Text=selected??"";
 }
 public override void OnApplyTemplate(){if(input!=null)input.TextChanged-=InputChanged;base.OnApplyTemplate();input=GetTemplateChild("PART_EditableTextBox") as TextBox;if(input!=null)input.TextChanged+=InputChanged;}
 void InputChanged(object sender,TextChangedEventArgs e){if(updating||!input.IsKeyboardFocusWithin||SelectedItem is string selected&&selected==input.Text)return;FilterQuery(input.Text);}
 public void FilterQuery(string query) {
  if(updating)return;updating=true;
  try {int caret=input?.CaretIndex??query.Length;ItemsSource=projects.Where(p=>matches(p,query)).ToArray();SelectedItem=null;Text=query;if(input!=null){input.Text=query;input.Select(Math.Min(caret,query.Length),0);}if(IsLoaded&&input?.IsKeyboardFocusWithin==true)IsDropDownOpen=true;}
  finally {updating=false;}
 }
 protected override void OnSelectionChanged(SelectionChangedEventArgs e){if(!updating&&SelectedItem!=null)base.OnSelectionChanged(e);}
}


