
//------------------------------------------------------------------------------

//  <auto-generated>
//      This code was generated by:
//        TerminalGuiDesigner v2.0.0.0
//      You can make changes to this file and they will not be overwritten when saving.
//  </auto-generated>
// -----------------------------------------------------------------------------
namespace RV.WebRTCForwarders {
    using System.Runtime.CompilerServices;
    using Terminal.Gui;
    
    
    public partial class EnterKeyForm {
        public string Key;
        public bool PlaceHolderErased = false;
        public EnterKeyForm() {
            InitializeComponent();
            System.EventHandler<Terminal.Gui.Key> ErasePlaceholder = (object _, Terminal.Gui.Key _) =>
            {
                if (PlaceHolderErased == false)
                {
                    PlaceHolderErased = true;
                    keyfield.Text = "";
                }
            };
            keyfield.SetFocus();
            keyfield.KeyDown += ErasePlaceholder;
            keyentered.Accept += (_, _) => {
                Key = keyfield.Text.ToUpperInvariant().Replace("0","O").Replace("1", "I").Trim();
                MessageBox.Query("Interpretation", $"Interpreted as {Key}", "");
                this.RequestStop();
            };
            clear.Accept += (_, _) =>
            {
                keyfield.Text = "";
                Key = "";
            };
        }
    }
}
