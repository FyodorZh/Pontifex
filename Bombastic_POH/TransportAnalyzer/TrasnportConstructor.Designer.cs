namespace TransportAnalyzer
{
    partial class TrasnportConstructor
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
            this.tabList = new System.Windows.Forms.TabControl();
            this.directTab = new System.Windows.Forms.TabPage();
            this.label1 = new System.Windows.Forms.Label();
            this.directServerName = new System.Windows.Forms.TextBox();
            this.tcpTab = new System.Windows.Forms.TabPage();
            this.tcpIp = new System.Windows.Forms.ComboBox();
            this.tcpPort = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.redisTab = new System.Windows.Forms.TabPage();
            this.label6 = new System.Windows.Forms.Label();
            this.redisServerName = new System.Windows.Forms.TextBox();
            this.udpTab = new System.Windows.Forms.TabPage();
            this.udpIp = new System.Windows.Forms.ComboBox();
            this.udpPort = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.checkReconnectable = new System.Windows.Forms.CheckBox();
            this.button1 = new System.Windows.Forms.Button();
            this.checkMonitor = new System.Windows.Forms.CheckBox();
            this.numClients = new System.Windows.Forms.NumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            this.checkCompress = new System.Windows.Forms.CheckBox();
            this.url = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.button3 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.btnNewServer = new System.Windows.Forms.Button();
            this.tabList.SuspendLayout();
            this.directTab.SuspendLayout();
            this.tcpTab.SuspendLayout();
            this.redisTab.SuspendLayout();
            this.udpTab.SuspendLayout();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numClients)).BeginInit();
            this.SuspendLayout();
            // 
            // tabList
            // 
            this.tabList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabList.Controls.Add(this.directTab);
            this.tabList.Controls.Add(this.tcpTab);
            this.tabList.Controls.Add(this.redisTab);
            this.tabList.Controls.Add(this.udpTab);
            this.tabList.Location = new System.Drawing.Point(12, 12);
            this.tabList.Name = "tabList";
            this.tabList.SelectedIndex = 0;
            this.tabList.Size = new System.Drawing.Size(434, 142);
            this.tabList.TabIndex = 5;
            // 
            // directTab
            // 
            this.directTab.Controls.Add(this.label1);
            this.directTab.Controls.Add(this.directServerName);
            this.directTab.Location = new System.Drawing.Point(4, 22);
            this.directTab.Name = "directTab";
            this.directTab.Padding = new System.Windows.Forms.Padding(3);
            this.directTab.Size = new System.Drawing.Size(426, 116);
            this.directTab.TabIndex = 0;
            this.directTab.Text = "Direct";
            this.directTab.UseVisualStyleBackColor = true;
            this.directTab.Enter += new System.EventHandler(this.directTab_Enter);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(17, 14);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(83, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "TransportName:";
            // 
            // directServerName
            // 
            this.directServerName.Location = new System.Drawing.Point(17, 33);
            this.directServerName.Name = "directServerName";
            this.directServerName.Size = new System.Drawing.Size(264, 20);
            this.directServerName.TabIndex = 2;
            this.directServerName.Text = "server1";
            // 
            // tcpTab
            // 
            this.tcpTab.Controls.Add(this.tcpIp);
            this.tcpTab.Controls.Add(this.tcpPort);
            this.tcpTab.Controls.Add(this.label4);
            this.tcpTab.Controls.Add(this.label3);
            this.tcpTab.Location = new System.Drawing.Point(4, 22);
            this.tcpTab.Name = "tcpTab";
            this.tcpTab.Padding = new System.Windows.Forms.Padding(3);
            this.tcpTab.Size = new System.Drawing.Size(426, 116);
            this.tcpTab.TabIndex = 1;
            this.tcpTab.Text = "Tcp";
            this.tcpTab.UseVisualStyleBackColor = true;
            this.tcpTab.Enter += new System.EventHandler(this.tcpTab_Enter);
            // 
            // tcpIp
            // 
            this.tcpIp.FormattingEnabled = true;
            this.tcpIp.Location = new System.Drawing.Point(76, 17);
            this.tcpIp.Name = "tcpIp";
            this.tcpIp.Size = new System.Drawing.Size(121, 21);
            this.tcpIp.TabIndex = 4;
            this.tcpIp.SelectedValueChanged += new System.EventHandler(this.tcpIp_SelectedValueChanged);
            // 
            // tcpPort
            // 
            this.tcpPort.Location = new System.Drawing.Point(75, 44);
            this.tcpPort.Name = "tcpPort";
            this.tcpPort.Size = new System.Drawing.Size(100, 20);
            this.tcpPort.TabIndex = 3;
            this.tcpPort.Text = "12345";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(6, 47);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(63, 13);
            this.label4.TabIndex = 1;
            this.label4.Text = "Server Port:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(15, 21);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(54, 13);
            this.label3.TabIndex = 0;
            this.label3.Text = "Server IP:";
            // 
            // redisTab
            // 
            this.redisTab.Controls.Add(this.label6);
            this.redisTab.Controls.Add(this.redisServerName);
            this.redisTab.Location = new System.Drawing.Point(4, 22);
            this.redisTab.Name = "redisTab";
            this.redisTab.Padding = new System.Windows.Forms.Padding(3);
            this.redisTab.Size = new System.Drawing.Size(426, 116);
            this.redisTab.TabIndex = 2;
            this.redisTab.Text = "Redis";
            this.redisTab.UseVisualStyleBackColor = true;
            this.redisTab.Enter += new System.EventHandler(this.redisTab_Enter);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(17, 14);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(83, 13);
            this.label6.TabIndex = 5;
            this.label6.Text = "TransportName:";
            // 
            // redisServerName
            // 
            this.redisServerName.Location = new System.Drawing.Point(17, 33);
            this.redisServerName.Name = "redisServerName";
            this.redisServerName.Size = new System.Drawing.Size(264, 20);
            this.redisServerName.TabIndex = 4;
            this.redisServerName.Text = "redisS1";
            // 
            // udpTab
            // 
            this.udpTab.Controls.Add(this.udpIp);
            this.udpTab.Controls.Add(this.udpPort);
            this.udpTab.Controls.Add(this.label7);
            this.udpTab.Controls.Add(this.label8);
            this.udpTab.Location = new System.Drawing.Point(4, 22);
            this.udpTab.Name = "udpTab";
            this.udpTab.Padding = new System.Windows.Forms.Padding(3);
            this.udpTab.Size = new System.Drawing.Size(426, 116);
            this.udpTab.TabIndex = 3;
            this.udpTab.Text = "Udp";
            this.udpTab.UseVisualStyleBackColor = true;
            this.udpTab.Enter += new System.EventHandler(this.udpTab_Enter);
            // 
            // udpIp
            // 
            this.udpIp.FormattingEnabled = true;
            this.udpIp.Location = new System.Drawing.Point(77, 18);
            this.udpIp.Name = "udpIp";
            this.udpIp.Size = new System.Drawing.Size(100, 21);
            this.udpIp.TabIndex = 8;
            this.udpIp.SelectedValueChanged += new System.EventHandler(this.udpIp_SelectedValueChanged);
            // 
            // udpPort
            // 
            this.udpPort.Location = new System.Drawing.Point(77, 45);
            this.udpPort.Name = "udpPort";
            this.udpPort.Size = new System.Drawing.Size(100, 20);
            this.udpPort.TabIndex = 7;
            this.udpPort.Text = "12345";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(8, 48);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(63, 13);
            this.label7.TabIndex = 5;
            this.label7.Text = "Server Port:";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(17, 22);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(54, 13);
            this.label8.TabIndex = 4;
            this.label8.Text = "Server IP:";
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.checkReconnectable);
            this.panel1.Controls.Add(this.button1);
            this.panel1.Controls.Add(this.checkMonitor);
            this.panel1.Controls.Add(this.numClients);
            this.panel1.Controls.Add(this.label5);
            this.panel1.Controls.Add(this.checkCompress);
            this.panel1.Controls.Add(this.url);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.button3);
            this.panel1.Controls.Add(this.button2);
            this.panel1.Controls.Add(this.btnNewServer);
            this.panel1.Location = new System.Drawing.Point(16, 160);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(430, 116);
            this.panel1.TabIndex = 6;
            // 
            // checkReconnectable
            // 
            this.checkReconnectable.AutoSize = true;
            this.checkReconnectable.Location = new System.Drawing.Point(313, 16);
            this.checkReconnectable.Name = "checkReconnectable";
            this.checkReconnectable.Size = new System.Drawing.Size(99, 17);
            this.checkReconnectable.TabIndex = 17;
            this.checkReconnectable.Text = "Reconnectable";
            this.checkReconnectable.UseVisualStyleBackColor = true;
            this.checkReconnectable.CheckedChanged += new System.EventHandler(this.checkCompress_CheckedChanged);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(313, 74);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 16;
            this.button1.Text = "MultiClient";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click_1);
            // 
            // checkMonitor
            // 
            this.checkMonitor.AutoSize = true;
            this.checkMonitor.Location = new System.Drawing.Point(245, 16);
            this.checkMonitor.Name = "checkMonitor";
            this.checkMonitor.Size = new System.Drawing.Size(61, 17);
            this.checkMonitor.TabIndex = 15;
            this.checkMonitor.Text = "Monitor";
            this.checkMonitor.UseVisualStyleBackColor = true;
            this.checkMonitor.CheckedChanged += new System.EventHandler(this.checkCompress_CheckedChanged);
            // 
            // numClients
            // 
            this.numClients.Location = new System.Drawing.Point(122, 12);
            this.numClients.Name = "numClients";
            this.numClients.Size = new System.Drawing.Size(38, 20);
            this.numClients.TabIndex = 14;
            this.numClients.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numClients.ValueChanged += new System.EventHandler(this.numClients_ValueChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(40, 16);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(76, 13);
            this.label5.TabIndex = 12;
            this.label5.Text = "Clients number";
            // 
            // checkCompress
            // 
            this.checkCompress.AutoSize = true;
            this.checkCompress.Location = new System.Drawing.Point(166, 16);
            this.checkCompress.Name = "checkCompress";
            this.checkCompress.Size = new System.Drawing.Size(72, 17);
            this.checkCompress.TabIndex = 11;
            this.checkCompress.Text = "Compress";
            this.checkCompress.UseVisualStyleBackColor = true;
            this.checkCompress.CheckedChanged += new System.EventHandler(this.checkCompress_CheckedChanged);
            // 
            // url
            // 
            this.url.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.url.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.url.Location = new System.Drawing.Point(43, 39);
            this.url.Name = "url";
            this.url.ReadOnly = true;
            this.url.Size = new System.Drawing.Size(369, 20);
            this.url.TabIndex = 9;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(14, 42);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(23, 13);
            this.label2.TabIndex = 8;
            this.label2.Text = "Url:";
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(224, 74);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(82, 23);
            this.button3.TabIndex = 7;
            this.button3.Text = "Server+Client";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(131, 74);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(87, 23);
            this.button2.TabIndex = 6;
            this.button2.Text = "Client";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // btnNewServer
            // 
            this.btnNewServer.Location = new System.Drawing.Point(42, 74);
            this.btnNewServer.Name = "btnNewServer";
            this.btnNewServer.Size = new System.Drawing.Size(83, 23);
            this.btnNewServer.TabIndex = 5;
            this.btnNewServer.Text = "Server";
            this.btnNewServer.UseVisualStyleBackColor = true;
            this.btnNewServer.Click += new System.EventHandler(this.button1_Click);
            // 
            // TrasnportConstructor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(452, 279);
            this.Controls.Add(this.tabList);
            this.Controls.Add(this.panel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "TrasnportConstructor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Trasnport C-tor";
            this.Load += new System.EventHandler(this.TrasnportConstructor_Load);
            this.tabList.ResumeLayout(false);
            this.directTab.ResumeLayout(false);
            this.directTab.PerformLayout();
            this.tcpTab.ResumeLayout(false);
            this.tcpTab.PerformLayout();
            this.redisTab.ResumeLayout(false);
            this.redisTab.PerformLayout();
            this.udpTab.ResumeLayout(false);
            this.udpTab.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numClients)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabList;
        private System.Windows.Forms.TabPage directTab;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox directServerName;
        private System.Windows.Forms.TabPage tcpTab;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button btnNewServer;
        private System.Windows.Forms.TextBox url;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tcpPort;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown numClients;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.CheckBox checkCompress;
        private System.Windows.Forms.TabPage redisTab;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox redisServerName;
        private System.Windows.Forms.CheckBox checkMonitor;
        private System.Windows.Forms.TabPage udpTab;
        private System.Windows.Forms.TextBox udpPort;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.ComboBox udpIp;
        private System.Windows.Forms.ComboBox tcpIp;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.CheckBox checkReconnectable;
    }
}