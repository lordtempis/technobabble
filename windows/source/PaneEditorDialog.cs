using System;
using System.Drawing;
using System.Windows.Forms;

namespace Technobabble {
    internal static class PaneEditorDialog {
        public static void Show(Form owner,Studio board){
            board.OpenPaneEditor();if(!board.Panes.IsOpen)return;
            try{
                using(var dialog=new Form()){
                    dialog.Text="Edit Studio panes";dialog.AutoScaleMode=AutoScaleMode.Font;
                    dialog.FormBorderStyle=FormBorderStyle.FixedDialog;dialog.MaximizeBox=false;dialog.MinimizeBox=false;
                    dialog.ShowInTaskbar=false;dialog.StartPosition=FormStartPosition.CenterParent;dialog.ClientSize=new Size(560,288);
                    var pane=new ComboBox{Left=12,Top=30,Width=536,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Selected pane",TabIndex=0};
                    var action=new ComboBox{Left=12,Top=80,Width=536,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Pane action",TabIndex=1};
                    action.Items.AddRange(new object[]{"Replace / swap widget","Split top / bottom","Split left / right"});
                    var widget=new ComboBox{Left=12,Top=130,Width=536,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Widget",TabIndex=2,DropDownWidth=650};
                    var info=new Label{Left=12,Top=163,Width=536,Height=31,Text="Edits pin all panes so your choices stay in place. Use P to unpin later."};
                    var message=new Label{Left=12,Top=195,Width=536,Height=38};
                    var apply=new Button{Text="Apply",Left=12,Top=244,Width=118,TabIndex=3};
                    var remove=new Button{Text="Remove pane",Left=140,Top=244,Width=128,TabIndex=4};
                    var undo=new Button{Text="Undo last edit",Left=278,Top=244,Width=132,TabIndex=5};
                    var cancel=new Button{Text="Cancel",Left=420,Top=244,Width=128,TabIndex=6,DialogResult=DialogResult.Cancel};
                    bool updating=false;
                    Action refresh=delegate{
                        updating=true;
                        try{
                            pane.Items.Clear();pane.Items.AddRange(board.Panes.PaneNames);pane.SelectedIndex=board.Panes.PaneIndex;
                            action.SelectedIndex=(int)board.Panes.Action;
                            widget.Items.Clear();widget.Items.AddRange(board.Panes.Choices);widget.SelectedIndex=board.Panes.Choice;
                            remove.Enabled=board.Panes.CanRemove;undo.Enabled=board.Panes.CanUndo;message.Text=board.Panes.Message??"";
                        }finally{updating=false;}
                    };
                    pane.SelectedIndexChanged+=delegate{if(!updating){board.Panes.SelectPane(pane.SelectedIndex);refresh();}};
                    action.SelectedIndexChanged+=delegate{if(!updating){board.Panes.SetAction((PaneEditAction)action.SelectedIndex);refresh();}};
                    widget.SelectedIndexChanged+=delegate{if(!updating)board.Panes.SetChoice(widget.SelectedIndex);};
                    apply.Click+=delegate{board.Panes.SetChoice(widget.SelectedIndex);if(board.ApplyPaneEdit())dialog.DialogResult=DialogResult.OK;else message.Text=board.Panes.Message;};
                    remove.Click+=delegate{if(board.RemovePane())dialog.DialogResult=DialogResult.OK;else message.Text=board.Panes.Message;};
                    undo.Click+=delegate{if(board.UndoPaneEdit())dialog.DialogResult=DialogResult.OK;else message.Text=board.Panes.Message;};
                    dialog.Controls.AddRange(new Control[]{
                        new Label{Text="Pane",Left=12,Top=12,AutoSize=true},pane,
                        new Label{Text="Action",Left=12,Top=62,AutoSize=true},action,
                        new Label{Text="Widget",Left=12,Top=112,AutoSize=true},widget,info,message,apply,remove,undo,cancel});
                    dialog.AcceptButton=apply;dialog.CancelButton=cancel;refresh();dialog.Shown+=delegate{widget.Focus();};
                    dialog.ShowDialog(owner);
                }
            }finally{board.Panes.Close();}
        }
    }
}
