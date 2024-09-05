
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
        
        private Terminal.Gui.Label label;
        
        private Terminal.Gui.TextField portnumber;
        
        private Terminal.Gui.RadioGroup role;
        
        private Terminal.Gui.Button calculatebutton;
        
        private Terminal.Gui.TextView confout;
        
        private void InitializeComponent() {
            this.confout = new Terminal.Gui.TextView();
            this.calculatebutton = new Terminal.Gui.Button();
            this.role = new Terminal.Gui.RadioGroup();
            this.portnumber = new Terminal.Gui.TextField();
            this.label = new Terminal.Gui.Label();
            this.blueOnBlack = new Terminal.Gui.ColorScheme(new Terminal.Gui.Attribute(4282087679u, 4278979596u), new Terminal.Gui.Attribute(4282087679u, 4294570405u), new Terminal.Gui.Attribute(4282029789u, 4278979596u), new Terminal.Gui.Attribute(4291611852u, 4278979596u), new Terminal.Gui.Attribute(4282029789u, 4294570405u));
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
            this.confout.Width = Dim.Fill(74);
            this.confout.Height = 27;
            this.confout.X = 1;
            this.confout.Y = 8;
            this.confout.Visible = true;
            this.confout.Arrangement = Terminal.Gui.ViewArrangement.Fixed;
            this.confout.AllowsTab = true;
            this.confout.AllowsReturn = true;
            this.confout.WordWrap = false;
            this.confout.Data = "confout";
            this.confout.Text = "";
            this.confout.TextAlignment = Terminal.Gui.Alignment.Start;
            this.Add(this.confout);
        }
    }
}