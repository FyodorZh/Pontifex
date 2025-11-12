namespace TransportAnalyzer
{
    partial class TransportAnalyzer
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
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.transportToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.startTransportToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.gCToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.transportToolStripMenuItem,
            this.gCToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(815, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // transportToolStripMenuItem
            // 
            this.transportToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.startTransportToolStripMenuItem});
            this.transportToolStripMenuItem.Name = "transportToolStripMenuItem";
            this.transportToolStripMenuItem.Size = new System.Drawing.Size(70, 20);
            this.transportToolStripMenuItem.Text = "Transport";
            // 
            // startTransportToolStripMenuItem
            // 
            this.startTransportToolStripMenuItem.Name = "startTransportToolStripMenuItem";
            this.startTransportToolStripMenuItem.Size = new System.Drawing.Size(98, 22);
            this.startTransportToolStripMenuItem.Text = "Start";
            this.startTransportToolStripMenuItem.Click += new System.EventHandler(this.startDirectTransportToolStripMenuItem_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Location = new System.Drawing.Point(0, 389);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(815, 22);
            this.statusStrip1.TabIndex = 1;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // gCToolStripMenuItem
            // 
            this.gCToolStripMenuItem.Name = "gCToolStripMenuItem";
            this.gCToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
            this.gCToolStripMenuItem.Text = "GC";
            this.gCToolStripMenuItem.Click += new System.EventHandler(this.gCToolStripMenuItem_Click);
            // 
            // TransportAnalyzer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(815, 411);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.IsMdiContainer = true;
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "TransportAnalyzer";
            this.Text = "TransportAnalyzer";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripMenuItem transportToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem startTransportToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem gCToolStripMenuItem;
    }
}

