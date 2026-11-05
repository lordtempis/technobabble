using System.Drawing;
using System.Windows.Forms;

namespace Technobabble {
    internal static class LayoutDialog {
        public static void Show(Form owner,Studio board,bool save){
            board.Open(save);
            ShowEditor(owner,board);
        }
        public static void Filter(Form owner,Studio board){board.OpenFilter();if(board.Editing)ShowEditor(owner,board);}
        public static void Network(Form owner,Studio board){board.OpenNetwork();ShowEditor(owner,board);}
        static void ShowEditor(Form owner,Studio board){
            try{
                using(var dialog=new Form()){
                    dialog.Text=board.EditorTitle;
                    dialog.AutoScaleMode=AutoScaleMode.Font;
                    dialog.FormBorderStyle=FormBorderStyle.FixedDialog;
                    dialog.MaximizeBox=false;dialog.MinimizeBox=false;dialog.ShowInTaskbar=false;
                    dialog.StartPosition=FormStartPosition.CenterParent;dialog.ClientSize=new Size(440,144);
                    var label=new Label{Text=board.EditorLabel,Left=12,Top=12,AutoSize=true};
                    var name=new ComboBox{AccessibleName=board.EditorLabel,Left=12,Top=31,Width=416,MaxLength=board.EditorLimit,TabIndex=0};
                    if(board.PickingNetwork)name.DropDownStyle=ComboBoxStyle.DropDownList;
                    name.Items.AddRange(board.Choices);name.Text=board.NameText;
                    var message=new Label{Left=12,Top=61,Width=416,Height=38,Text=board.EditorHint};
                    var ok=new Button{Text=board.AcceptLabel,Left=264,Top=109,Width=78,TabIndex=1};
                    var cancel=new Button{Text="Cancel",Left=350,Top=109,Width=78,TabIndex=2,DialogResult=DialogResult.Cancel};
                    name.TextChanged+=delegate{board.SetName(name.Text);message.Text="";};
                    ok.Click+=delegate{
                        // DropDownList selection need not raise TextChanged. Apply the
                        // displayed adapter explicitly; editable layouts keep their overwrite state.
                        if(board.PickingNetwork)board.SetName(name.Text);
                        board.Submit();
                        if(board.Editing)message.Text=board.Error;
                        else dialog.DialogResult=DialogResult.OK;
                    };
                    dialog.Controls.AddRange(new Control[]{label,name,message,ok,cancel});
                    dialog.AcceptButton=ok;dialog.CancelButton=cancel;
                    dialog.Shown+=delegate{name.Focus();name.SelectAll();};
                    dialog.ShowDialog(owner);
                }
            }finally{board.Cancel();}
        }
    }
}
