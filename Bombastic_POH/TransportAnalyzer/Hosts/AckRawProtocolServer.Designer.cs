namespace TransportAnalyzer.Hosts
{
    partial class AckRawProtocolServer
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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.clientList = new System.Windows.Forms.ListBox();
            this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.disconnectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.logBox = new System.Windows.Forms.ListBox();
            this.stopTransportToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.contextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.clientList);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.logBox);
            this.splitContainer1.Size = new System.Drawing.Size(800, 450);
            this.splitContainer1.SplitterDistance = 266;
            this.splitContainer1.TabIndex = 0;
            // 
            // clientList
            // 
            this.clientList.ContextMenuStrip = this.contextMenuStrip;
            this.clientList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.clientList.FormattingEnabled = true;
            this.clientList.Location = new System.Drawing.Point(0, 0);
            this.clientList.Name = "clientList";
            this.clientList.Size = new System.Drawing.Size(266, 450);
            this.clientList.TabIndex = 0;
            this.clientList.MouseDown += new System.Windows.Forms.MouseEventHandler(this.clientList_MouseDown);
            // 
            // contextMenuStrip
            // 
            this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.disconnectToolStripMenuItem,
            this.stopTransportToolStripMenuItem});
            this.contextMenuStrip.Name = "contextMenuStrip";
            this.contextMenuStrip.Size = new System.Drawing.Size(181, 70);
            // 
            // disconnectToolStripMenuItem
            // 
            this.disconnectToolStripMenuItem.Name = "disconnectToolStripMenuItem";
            this.disconnectToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.disconnectToolStripMenuItem.Text = "Stop Protocol";
            this.disconnectToolStripMenuItem.Click += new System.EventHandler(this.stopProtocolToolStripMenuItem_Click);
            // 
            // logBox
            // 
            this.logBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logBox.FormattingEnabled = true;
            this.logBox.Location = new System.Drawing.Point(0, 0);
            this.logBox.Name = "logBox";
            this.logBox.Size = new System.Drawing.Size(530, 450);
            this.logBox.TabIndex = 0;
            // 
            // stopTransportToolStripMenuItem
            // 
            this.stopTransportToolStripMenuItem.Name = "stopTransportToolStripMenuItem";
            this.stopTransportToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.stopTransportToolStripMenuItem.Text = "Stop Transport";
            this.stopTransportToolStripMenuItem.Click += new System.EventHandler(this.stopTransportToolStripMenuItem_Click);
            // 
            // AckRawProtocolServer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.splitContainer1);
            this.Name = "AckRawProtocolServer";
            this.Text = "AckRawProtocolServer";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.AckRawProtocolServer_FormClosing);
            this.Load += new System.EventHandler(this.AckRawProtocolServer_Load);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.contextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListBox logBox;
        private System.Windows.Forms.ListBox clientList;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem disconnectToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem stopTransportToolStripMenuItem;
    }
}