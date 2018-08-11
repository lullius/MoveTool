namespace RealtimeEditor
{
    partial class MainForm
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
            this.lbOutput = new System.Windows.Forms.ListBox();
            this.bScan = new System.Windows.Forms.Button();
            this.cbBAC = new System.Windows.Forms.ComboBox();
            this.cbBCM = new System.Windows.Forms.ComboBox();
            this.lBAC = new System.Windows.Forms.Label();
            this.lBCM = new System.Windows.Forms.Label();
            this.bSelectBACJson = new System.Windows.Forms.Button();
            this.bSelectBCMJson = new System.Windows.Forms.Button();
            this.bRestore = new System.Windows.Forms.Button();
            this.gbBAC = new System.Windows.Forms.GroupBox();
            this.gbBCM = new System.Windows.Forms.GroupBox();
            this.gbBAC.SuspendLayout();
            this.gbBCM.SuspendLayout();
            this.SuspendLayout();
            // 
            // lbOutput
            // 
            this.lbOutput.FormattingEnabled = true;
            this.lbOutput.Location = new System.Drawing.Point(12, 228);
            this.lbOutput.Name = "lbOutput";
            this.lbOutput.Size = new System.Drawing.Size(476, 160);
            this.lbOutput.TabIndex = 2;
            // 
            // bScan
            // 
            this.bScan.Location = new System.Drawing.Point(200, 33);
            this.bScan.Name = "bScan";
            this.bScan.Size = new System.Drawing.Size(101, 23);
            this.bScan.TabIndex = 3;
            this.bScan.Text = "Scan";
            this.bScan.UseVisualStyleBackColor = true;
            this.bScan.Click += new System.EventHandler(this.OnClick_ScanButton);
            // 
            // cbBAC
            // 
            this.cbBAC.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbBAC.FormattingEnabled = true;
            this.cbBAC.Location = new System.Drawing.Point(9, 21);
            this.cbBAC.Name = "cbBAC";
            this.cbBAC.Size = new System.Drawing.Size(167, 21);
            this.cbBAC.TabIndex = 4;
            this.cbBAC.SelectedIndexChanged += new System.EventHandler(this.cbBAC_SelectedIndexChanged);
            // 
            // cbBCM
            // 
            this.cbBCM.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbBCM.FormattingEnabled = true;
            this.cbBCM.Location = new System.Drawing.Point(9, 21);
            this.cbBCM.Name = "cbBCM";
            this.cbBCM.Size = new System.Drawing.Size(164, 21);
            this.cbBCM.TabIndex = 5;
            this.cbBCM.SelectedIndexChanged += new System.EventHandler(this.cbBCM_SelectedIndexChanged);
            // 
            // lBAC
            // 
            this.lBAC.Location = new System.Drawing.Point(6, 84);
            this.lBAC.Name = "lBAC";
            this.lBAC.Size = new System.Drawing.Size(170, 114);
            this.lBAC.TabIndex = 6;
            this.lBAC.Text = "No JSON-file set.";
            // 
            // lBCM
            // 
            this.lBCM.Location = new System.Drawing.Point(6, 84);
            this.lBCM.Name = "lBCM";
            this.lBCM.Size = new System.Drawing.Size(169, 114);
            this.lBCM.TabIndex = 7;
            this.lBCM.Text = "No JSON-file set.";
            // 
            // bSelectBACJson
            // 
            this.bSelectBACJson.Location = new System.Drawing.Point(6, 48);
            this.bSelectBACJson.Name = "bSelectBACJson";
            this.bSelectBACJson.Size = new System.Drawing.Size(170, 23);
            this.bSelectBACJson.TabIndex = 8;
            this.bSelectBACJson.Text = "Select Json";
            this.bSelectBACJson.UseVisualStyleBackColor = true;
            this.bSelectBACJson.Click += new System.EventHandler(this.bSelectBACJson_Click);
            // 
            // bSelectBCMJson
            // 
            this.bSelectBCMJson.Location = new System.Drawing.Point(9, 48);
            this.bSelectBCMJson.Name = "bSelectBCMJson";
            this.bSelectBCMJson.Size = new System.Drawing.Size(164, 23);
            this.bSelectBCMJson.TabIndex = 9;
            this.bSelectBCMJson.Text = "Select Json";
            this.bSelectBCMJson.UseVisualStyleBackColor = true;
            this.bSelectBCMJson.Click += new System.EventHandler(this.bSelectBCMJson_Click);
            // 
            // bRestore
            // 
            this.bRestore.Location = new System.Drawing.Point(200, 60);
            this.bRestore.Name = "bRestore";
            this.bRestore.Size = new System.Drawing.Size(101, 23);
            this.bRestore.TabIndex = 10;
            this.bRestore.Text = "Restore Game";
            this.bRestore.UseVisualStyleBackColor = true;
            this.bRestore.Click += new System.EventHandler(this.bRestore_Click);
            // 
            // gbBAC
            // 
            this.gbBAC.Controls.Add(this.bSelectBACJson);
            this.gbBAC.Controls.Add(this.cbBAC);
            this.gbBAC.Controls.Add(this.lBAC);
            this.gbBAC.Location = new System.Drawing.Point(12, 12);
            this.gbBAC.Name = "gbBAC";
            this.gbBAC.Size = new System.Drawing.Size(182, 210);
            this.gbBAC.TabIndex = 11;
            this.gbBAC.TabStop = false;
            this.gbBAC.Text = "BAC";
            // 
            // gbBCM
            // 
            this.gbBCM.Controls.Add(this.bSelectBCMJson);
            this.gbBCM.Controls.Add(this.lBCM);
            this.gbBCM.Controls.Add(this.cbBCM);
            this.gbBCM.Location = new System.Drawing.Point(307, 12);
            this.gbBCM.Name = "gbBCM";
            this.gbBCM.Size = new System.Drawing.Size(181, 210);
            this.gbBCM.TabIndex = 12;
            this.gbBCM.TabStop = false;
            this.gbBCM.Text = "BCM";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(500, 400);
            this.Controls.Add(this.gbBCM);
            this.Controls.Add(this.gbBAC);
            this.Controls.Add(this.bRestore);
            this.Controls.Add(this.bScan);
            this.Controls.Add(this.lbOutput);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.ShowIcon = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "Real-time editor";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.gbBAC.ResumeLayout(false);
            this.gbBCM.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox lbOutput;
        private System.Windows.Forms.Button bScan;
        private System.Windows.Forms.ComboBox cbBAC;
        private System.Windows.Forms.ComboBox cbBCM;
        private System.Windows.Forms.Label lBAC;
        private System.Windows.Forms.Label lBCM;
        private System.Windows.Forms.Button bSelectBACJson;
        private System.Windows.Forms.Button bSelectBCMJson;
        private System.Windows.Forms.Button bRestore;
        private System.Windows.Forms.GroupBox gbBAC;
        private System.Windows.Forms.GroupBox gbBCM;
    }
}

