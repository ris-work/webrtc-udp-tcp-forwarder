
//------------------------------------------------------------------------------

//  <auto-generated>
//      This code was generated by:
//        TerminalGuiDesigner v2.0.0.0
//      Changes to this file may cause incorrect behavior and will be lost if
//      the code is regenerated.
//  </auto-generated>
// -----------------------------------------------------------------------------
namespace RV.WebRTCForwarders {
    using System;
    using Terminal.Gui;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Drawing;
    
    
    public partial class PortNumberCalculationUtils : Terminal.Gui.Window {
        
        private Terminal.Gui.ColorScheme blueOnBlack;
        
        private Terminal.Gui.ColorScheme greenOnBlack;
        
        private Terminal.Gui.ColorScheme new1;
        
        private Terminal.Gui.Label label;
        
        private Terminal.Gui.TextField portnumber;
        
        private Terminal.Gui.RadioGroup role;
        
        private Terminal.Gui.Button calculatebutton;
        
        private Terminal.Gui.Button genkeysbutton;
        
        private Terminal.Gui.Label labelOurs;
        
        private Terminal.Gui.Label privKeyOurs;
        
        private Terminal.Gui.Label pubKeyOurs;
        
        private Terminal.Gui.Label labelTheirs;
        
        private Terminal.Gui.Label privKeyTheirs;
        
        private Terminal.Gui.Label pubKeyTheirs;
        
        private Terminal.Gui.TextView confout;
        
        private Terminal.Gui.TextView confoutTheirs;
        
        private void InitializeComponent() {
            this.confoutTheirs = new Terminal.Gui.TextView();
            this.confout = new Terminal.Gui.TextView();
            this.pubKeyTheirs = new Terminal.Gui.Label();
            this.privKeyTheirs = new Terminal.Gui.Label();
            this.labelTheirs = new Terminal.Gui.Label();
            this.pubKeyOurs = new Terminal.Gui.Label();
            this.privKeyOurs = new Terminal.Gui.Label();
            this.labelOurs = new Terminal.Gui.Label();
            this.genkeysbutton = new Terminal.Gui.Button();
            this.calculatebutton = new Terminal.Gui.Button();
            this.role = new Terminal.Gui.RadioGroup();
            this.portnumber = new Terminal.Gui.TextField();
            this.label = new Terminal.Gui.Label();
            this.blueOnBlack = new Terminal.Gui.ColorScheme(new Terminal.Gui.Attribute(4282087679u, 4278979596u), new Terminal.Gui.Attribute(4282087679u, 4294570405u), new Terminal.Gui.Attribute(4282029789u, 4278979596u), new Terminal.Gui.Attribute(4291611852u, 4278979596u), new Terminal.Gui.Attribute(4282029789u, 4294570405u));
            this.greenOnBlack = new Terminal.Gui.ColorScheme(new Terminal.Gui.Attribute(4279476494u, 4278979596u), new Terminal.Gui.Attribute(4279476494u, 4287109016u), new Terminal.Gui.Attribute(4279682572u, 4278979596u), new Terminal.Gui.Attribute(4291611852u, 4278979596u), new Terminal.Gui.Attribute(4279682572u, 4287109016u));
            this.new1 = new Terminal.Gui.ColorScheme(new Terminal.Gui.Attribute(4280927999u, 4291415892u), new Terminal.Gui.Attribute(4283567602u, 4282400832u), new Terminal.Gui.Attribute(4294111986u, 4278979596u), new Terminal.Gui.Attribute(4294306795u, 4289829530u), new Terminal.Gui.Attribute(4294111986u, 4278979596u));
            this.Width = Dim.Fill(0);
            this.Height = Dim.Fill(0);
            this.X = 0;
            this.Y = 0;
            this.Visible = true;
            this.Arrangement = (Terminal.Gui.ViewArrangement.Movable | Terminal.Gui.ViewArrangement.Overlapped);
            this.Modal = false;
            this.TextAlignment = Terminal.Gui.Alignment.Start;
            this.Title = "";
            this.label.Width = Dim.Auto();
            this.label.Height = 1;
            this.label.X = 1;
            this.label.Y = 1;
            this.label.Visible = true;
            this.label.Arrangement = Terminal.Gui.ViewArrangement.Fixed;
            this.label.Data = "label";
            this.label.Text = "Port number";
            this.label.TextAlignment = Terminal.Gui.Alignment.Start;
            this.Add(this.label);
            this.portnumber.Width = Dim.Fill(5);
            this.portnumber.Height = 1;
            this.portnumber.X = 18;
            this.portnumber.Y = 1;
            this.portnumber.Visible = true;
            this.portnumber.Arrangement = Terminal.Gui.ViewArrangement.Fixed;
            this.portnumber.Secret = false;
            this.portnumber.Data = "portnumber";
            this.portnumber.Text = "Port number";
            this.portnumber.TextAlignment = Terminal.Gui.Alignment.Start;
            this.Add(this.portnumber);
            this.role.Width = 10;
            this.role.Height = 2;
            this.role.X = 18;
            this.role.Y = 3;
            this.role.Visible = true;
            this.role.Arrangement = Terminal.Gui.ViewArrangement.Fixed;
            this.role.Data = "role";
            this.role.TextAlignment = Terminal.Gui.Alignment.Start;
            this.role.RadioLabels = new string[] {
                    "Server",
                    "Client"};
            this.Add(this.role);
            this.calculatebutton.Width = Dim.Auto();
            this.calculatebutton.Height = 1;
            this.calculatebutton.X = 9;
            this.calculatebutton.Y = 6;
            this.calculatebutton.Visible = true;
            this.calculatebutton.Arrangement = Terminal.Gui.ViewArrangement.Fixed;
            this.calculatebutton.ColorScheme = this.blueOnBlack;
            this.calculatebutton.Data = "calculatebutton";
            this.calculatebutton.Text = "Calculate";
            this.calculatebutton.TextAlignment = Terminal.Gui.Alignment.Center;
            this.calculatebutton.IsDefault = false;
            this.Add(this.calculatebutton);
            this.genkeysbutton.Width = Dim.Auto();
            this.genkeysbutton.Height = 1;
            this.genkeysbutton.X = 35;
            this.genkeysbutton.Y = 6;
            this.genkeysbutton.Visible = true;
            this.genkeysbutton.Arrangement = Terminal.Gui.ViewArrangement.Fixed;
            this.genkeysbutton.ColorScheme = this.new1;
            this.genkeysbutton.Data = "genkeysbutton";
            this.genkeysbutton.Text = "Generate keys (both sides) - requires WG";
            this.genkeysbutton.TextAlignment = Terminal.Gui.Alignment.Center;
            this.genkeysbutton.IsDefault = false;
            this.Add(this.genkeysbutton);
            this.labelOurs.Width = Dim.Auto();
            this.labelOurs.Height = 1;
            this.labelOurs.X = 89;
            this.labelOurs.Y = 6;
            this.labelOurs.Visible = true;
            this.labelOurs.Arrangement = Terminal.Gui.ViewArrangement.Fixed;
            this.labelOurs.Data = "labelOurs";
            this.labelOurs.Text = "Ours (priv, pub)";
            this.labelOurs.TextAlignment = Terminal.Gui.Alignment.Start;
            this.Add(this.labelOurs);
            this.privKeyOurs.Width = Dim.Auto();
            this.privKeyOurs.Height = 1;
            this.privKeyOurs.X = 113;
            this.privKeyOurs.Y = 6;
            this.privKeyOurs.Visible = true;
            this.privKeyOurs.Arrangement = Terminal.Gui.ViewArrangement.Fixed;
            this.privKeyOurs.Data = "privKeyOurs";
            this.privKeyOurs.Text = "PrivKeyOurs";
            this.privKeyOurs.TextAlignment = Terminal.Gui.Alignment.Start;
            this.Add(this.privKeyOurs);
            this.pubKeyOurs.Width = Dim.Auto();
            this.pubKeyOurs.Height = 1;
            this.pubKeyOurs.X = 113;
            this.pubKeyOurs.Y = 8;
            this.pubKeyOurs.Visible = true;
            this.pubKeyOurs.Arrangement = Terminal.Gui.ViewArrangement.Fixed;
            this.pubKeyOurs.Data = "pubKeyOurs";
            this.pubKeyOurs.Text = "PubKeyOurs";
            this.pubKeyOurs.TextAlignment = Terminal.Gui.Alignment.Start;
            this.Add(this.pubKeyOurs);
            this.labelTheirs.Width = Dim.Auto();
            this.labelTheirs.Height = 1;
            this.labelTheirs.X = 89;
            this.labelTheirs.Y = 10;
            this.labelTheirs.Visible = true;
            this.labelTheirs.Arrangement = Terminal.Gui.ViewArrangement.Fixed;
            this.labelTheirs.Data = "labelTheirs";
            this.labelTheirs.Text = "Theirs (priv, pub)";
            this.labelTheirs.TextAlignment = Terminal.Gui.Alignment.Start;
            this.Add(this.labelTheirs);
            this.privKeyTheirs.Width = Dim.Auto();
            this.privKeyTheirs.Height = 1;
            this.privKeyTheirs.X = 113;
            this.privKeyTheirs.Y = 10;
            this.privKeyTheirs.Visible = true;
            this.privKeyTheirs.Arrangement = Terminal.Gui.ViewArrangement.Fixed;
            this.privKeyTheirs.Data = "privKeyTheirs";
            this.privKeyTheirs.Text = "PrivKeyTheirs";
            this.privKeyTheirs.TextAlignment = Terminal.Gui.Alignment.Start;
            this.Add(this.privKeyTheirs);
            this.pubKeyTheirs.Width = Dim.Auto();
            this.pubKeyTheirs.Height = 1;
            this.pubKeyTheirs.X = 113;
            this.pubKeyTheirs.Y = 12;
            this.pubKeyTheirs.Visible = true;
            this.pubKeyTheirs.Arrangement = Terminal.Gui.ViewArrangement.Fixed;
            this.pubKeyTheirs.Data = "pubKeyTheirs";
            this.pubKeyTheirs.Text = "PubKeyTheirs";
            this.pubKeyTheirs.TextAlignment = Terminal.Gui.Alignment.Start;
            this.Add(this.pubKeyTheirs);
            this.confout.Width = Dim.Fill(5);
            this.confout.Height = 10;
            this.confout.X = 1;
            this.confout.Y = 14;
            this.confout.Visible = true;
            this.confout.Arrangement = Terminal.Gui.ViewArrangement.Fixed;
            this.confout.AllowsTab = true;
            this.confout.AllowsReturn = true;
            this.confout.WordWrap = false;
            this.confout.Data = "confout";
            this.confout.Text = "";
            this.confout.TextAlignment = Terminal.Gui.Alignment.Start;
            this.Add(this.confout);
            this.confoutTheirs.Width = Dim.Fill(5);
            this.confoutTheirs.Height = 10;
            this.confoutTheirs.X = 1;
            this.confoutTheirs.Y = 26;
            this.confoutTheirs.Visible = true;
            this.confoutTheirs.Arrangement = Terminal.Gui.ViewArrangement.Fixed;
            this.confoutTheirs.AllowsTab = true;
            this.confoutTheirs.AllowsReturn = true;
            this.confoutTheirs.WordWrap = false;
            this.confoutTheirs.Data = "confoutTheirs";
            this.confoutTheirs.Text = "";
            this.confoutTheirs.TextAlignment = Terminal.Gui.Alignment.Start;
            this.Add(this.confoutTheirs);
        }
    }
}
