namespace TransportAnalyzer.Hosts
{
    partial class AckRawProtocolClient
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
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.protocolGracefulStopBtn = new System.Windows.Forms.Button();
            this.transportStopBtn = new System.Windows.Forms.Button();
            this.protocolStopBtn = new System.Windows.Forms.Button();
            this.logBox = new System.Windows.Forms.ListBox();
            this.timer = new System.Windows.Forms.Timer(this.components);
            this.timer1000 = new System.Windows.Forms.Timer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.SuspendLayout();
            //
            // splitContainer1
            //
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            //
            // splitContainer1.Panel1
            //
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
            //
            // splitContainer1.Panel2
            //
            this.splitContainer1.Panel2.Controls.Add(this.logBox);
            this.splitContainer1.Size = new System.Drawing.Size(867, 462);
            this.splitContainer1.SplitterDistance = 233;
            this.splitContainer1.TabIndex = 0;
            //
            // splitContainer2
            //
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            //
            // splitContainer2.Panel1
            //
            this.splitContainer2.Panel1.Controls.Add(this.protocolGracefulStopBtn);
            this.splitContainer2.Panel1.Controls.Add(this.transportStopBtn);
            this.splitContainer2.Panel1.Controls.Add(this.protocolStopBtn);
            this.splitContainer2.Size = new System.Drawing.Size(867, 233);
            this.splitContainer2.SplitterDistance = 166;
            this.splitContainer2.TabIndex = 0;
            //
            // protocolGracefulStopBtn
            //
            this.protocolGracefulStopBtn.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.protocolGracefulStopBtn.Location = new System.Drawing.Point(12, 12);
            this.protocolGracefulStopBtn.Name = "protocolGracefulStopBtn";
            this.protocolGracefulStopBtn.Size = new System.Drawing.Size(143, 23);
            this.protocolGracefulStopBtn.TabIndex = 2;
            this.protocolGracefulStopBtn.Text = "Graceful Protocol Stop";
            this.protocolGracefulStopBtn.UseVisualStyleBackColor = true;
            this.protocolGracefulStopBtn.Click += new System.EventHandler(this.protocolGracefulStopBtn_Click);
            //
            // transportStopBtn
            //
            this.transportStopBtn.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.transportStopBtn.Location = new System.Drawing.Point(12, 70);
            this.transportStopBtn.Name = "transportStopBtn";
            this.transportStopBtn.Size = new System.Drawing.Size(143, 23);
            this.transportStopBtn.TabIndex = 1;
            this.transportStopBtn.Text = "Transport Stop";
            this.transportStopBtn.UseVisualStyleBackColor = true;
            this.transportStopBtn.Click += new System.EventHandler(this.transportStopBtn_Click);
            //
            // protocolStopBtn
            //
            this.protocolStopBtn.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.protocolStopBtn.Location = new System.Drawing.Point(12, 41);
            this.protocolStopBtn.Name = "protocolStopBtn";
            this.protocolStopBtn.Size = new System.Drawing.Size(143, 23);
            this.protocolStopBtn.TabIndex = 0;
            this.protocolStopBtn.Text = "Protocol Stop";
            this.protocolStopBtn.UseVisualStyleBackColor = true;
            this.protocolStopBtn.Click += new System.EventHandler(this.protocolStopBtn_Click);
            //
            // logBox
            //
            this.logBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logBox.FormattingEnabled = true;
            this.logBox.Location = new System.Drawing.Point(0, 0);
            this.logBox.Name = "logBox";
            this.logBox.Size = new System.Drawing.Size(867, 225);
            this.logBox.TabIndex = 0;
            //
            // timer
            //
            this.timer.Enabled = true;
            this.timer.Interval = 5;
            this.timer.Tick += new System.EventHandler(this.timer_Tick);
            //
            // timer1000
            //
            this.timer1000.Enabled = true;
            this.timer1000.Interval = 1000;
            this.timer1000.Tick += new System.EventHandler(this.timer1000_Tick);
            //
            // AckRawProtocolClient
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(867, 462);
            this.Controls.Add(this.splitContainer1);
            this.Name = "AckRawProtocolClient";
            this.Text = "AckRawProtocolClient";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.AckRawProtocolClient_FormClosing);
            this.Load += new System.EventHandler(this.AckRawProtocolClient_Load);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListBox logBox;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.Button protocolGracefulStopBtn;
        private System.Windows.Forms.Button transportStopBtn;
        private System.Windows.Forms.Button protocolStopBtn;
        private System.Windows.Forms.Timer timer;
        private System.Windows.Forms.Timer timer1000;
    }
}