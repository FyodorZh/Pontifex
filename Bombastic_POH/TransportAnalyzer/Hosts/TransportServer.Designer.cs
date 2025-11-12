namespace TransportAnalyzer
{
    partial class TransportServer
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.log = new System.Windows.Forms.ListBox();
            this.clientList = new System.Windows.Forms.ListBox();
            this.clientListMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.disconnectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.clientListMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // log
            // 
            this.log.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.log.FormattingEnabled = true;
            this.log.Location = new System.Drawing.Point(283, 12);
            this.log.Name = "log";
            this.log.Size = new System.Drawing.Size(621, 342);
            this.log.TabIndex = 0;
            // 
            // clientList
            // 
            this.clientList.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.clientList.ContextMenuStrip = this.clientListMenu;
            this.clientList.FormattingEnabled = true;
            this.clientList.Location = new System.Drawing.Point(12, 12);
            this.clientList.Name = "clientList";
            this.clientList.Size = new System.Drawing.Size(265, 342);
            this.clientList.TabIndex = 1;
            this.clientList.MouseDown += new System.Windows.Forms.MouseEventHandler(this.clientList_MouseDown);
            // 
            // clientListMenu
            // 
            this.clientListMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.disconnectToolStripMenuItem});
            this.clientListMenu.Name = "clientListMenu";
            this.clientListMenu.Size = new System.Drawing.Size(134, 26);
            // 
            // disconnectToolStripMenuItem
            // 
            this.disconnectToolStripMenuItem.Name = "disconnectToolStripMenuItem";
            this.disconnectToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.disconnectToolStripMenuItem.Text = "Disconnect";
            this.disconnectToolStripMenuItem.Click += new System.EventHandler(this.disconnectToolStripMenuItem_Click);
            // 
            // TransportServer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(916, 362);
            this.Controls.Add(this.clientList);
            this.Controls.Add(this.log);
            this.Name = "TransportServer";
            this.Text = "TransportServer";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.TransportServer_FormClosed);
            this.Load += new System.EventHandler(this.TransportServer_Load);
            this.clientListMenu.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox log;
        private System.Windows.Forms.ListBox clientList;
        private System.Windows.Forms.ContextMenuStrip clientListMenu;
        private System.Windows.Forms.ToolStripMenuItem disconnectToolStripMenuItem;
    }
}