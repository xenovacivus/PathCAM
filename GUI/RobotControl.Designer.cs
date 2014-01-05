namespace GUI
{
    partial class RobotControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.steppersEnabledBox = new System.Windows.Forms.CheckBox();
            this.cancelButton = new System.Windows.Forms.Button();
            this.zbox = new System.Windows.Forms.TextBox();
            this.pause_resume_button = new System.Windows.Forms.Button();
            this.zGo = new System.Windows.Forms.Button();
            this.runButton = new System.Windows.Forms.Button();
            this.button4 = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // steppersEnabledBox
            // 
            this.steppersEnabledBox.AutoCheck = false;
            this.steppersEnabledBox.AutoSize = true;
            this.steppersEnabledBox.Enabled = false;
            this.steppersEnabledBox.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.steppersEnabledBox.Location = new System.Drawing.Point(6, 103);
            this.steppersEnabledBox.Name = "steppersEnabledBox";
            this.steppersEnabledBox.Size = new System.Drawing.Size(107, 17);
            this.steppersEnabledBox.TabIndex = 3;
            this.steppersEnabledBox.Text = "Steppers Enabled";
            this.steppersEnabledBox.UseVisualStyleBackColor = true;
            this.steppersEnabledBox.Click += new System.EventHandler(this.steppersEnabledBox_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.Enabled = false;
            this.cancelButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cancelButton.Location = new System.Drawing.Point(87, 45);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 2;
            this.cancelButton.Text = "Clear";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // zbox
            // 
            this.zbox.Enabled = false;
            this.zbox.Location = new System.Drawing.Point(65, 76);
            this.zbox.Name = "zbox";
            this.zbox.Size = new System.Drawing.Size(97, 20);
            this.zbox.TabIndex = 78;
            this.zbox.Text = "0";
            // 
            // pause_resume_button
            // 
            this.pause_resume_button.Enabled = false;
            this.pause_resume_button.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.pause_resume_button.Location = new System.Drawing.Point(6, 45);
            this.pause_resume_button.Name = "pause_resume_button";
            this.pause_resume_button.Size = new System.Drawing.Size(75, 23);
            this.pause_resume_button.TabIndex = 1;
            this.pause_resume_button.Text = "Pause";
            this.pause_resume_button.UseVisualStyleBackColor = true;
            this.pause_resume_button.Click += new System.EventHandler(this.pause_resume_button_Click);
            // 
            // zGo
            // 
            this.zGo.Enabled = false;
            this.zGo.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.zGo.Location = new System.Drawing.Point(6, 74);
            this.zGo.Name = "zGo";
            this.zGo.Size = new System.Drawing.Size(53, 23);
            this.zGo.TabIndex = 77;
            this.zGo.Text = "ZGo";
            this.zGo.UseVisualStyleBackColor = true;
            this.zGo.Click += new System.EventHandler(this.zGo_Click);
            // 
            // runButton
            // 
            this.runButton.Enabled = false;
            this.runButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.runButton.Location = new System.Drawing.Point(87, 16);
            this.runButton.Name = "runButton";
            this.runButton.Size = new System.Drawing.Size(75, 23);
            this.runButton.TabIndex = 0;
            this.runButton.Text = "Run";
            this.runButton.UseVisualStyleBackColor = true;
            this.runButton.Click += new System.EventHandler(this.runButton_Click);
            // 
            // button4
            // 
            this.button4.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button4.Location = new System.Drawing.Point(6, 16);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(75, 23);
            this.button4.TabIndex = 79;
            this.button4.Text = "Com Port";
            this.button4.UseVisualStyleBackColor = true;
            this.button4.Click += new System.EventHandler(this.button4_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(96, 13);
            this.label1.TabIndex = 80;
            this.label1.Text = "--------ROBOT!--------";
            // 
            // RobotControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.label1);
            this.Controls.Add(this.button4);
            this.Controls.Add(this.runButton);
            this.Controls.Add(this.zGo);
            this.Controls.Add(this.steppersEnabledBox);
            this.Controls.Add(this.pause_resume_button);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.zbox);
            this.Name = "RobotControl";
            this.Size = new System.Drawing.Size(167, 124);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox steppersEnabledBox;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.TextBox zbox;
        private System.Windows.Forms.Button pause_resume_button;
        private System.Windows.Forms.Button zGo;
        private System.Windows.Forms.Button runButton;
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.Label label1;

    }
}
