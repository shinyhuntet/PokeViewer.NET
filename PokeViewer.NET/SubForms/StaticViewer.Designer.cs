using static System.Windows.Forms.DataFormats;

namespace PokeViewer.NET.SubForms
{
    partial class StaticViewer
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
            StopConditions = new Button();
            Language = new ComboBox();
            button1 = new Button();
            HardStopButton = new Button();
            checkBox4 = new CheckBox();
            checkBox5 = new CheckBox();
            Item7Label = new Label();
            XCoord = new TextBox();
            Item8Label = new Label();
            YCoord = new TextBox();
            Item9Label = new Label();
            ZCoord = new TextBox();
            pictureBox1 = new PictureBox();
            textBox1 = new TextBox();
            pictureBox2 = new PictureBox();
            TeleportIndex = new NumericUpDown();
            TeleportLabel = new Label();
            TeleportButton = new Button();
            LanguageLabel = new Label();
            ReadCoordButton = new Button();
            TeleportCoordButton = new Button();
            pictureBox3 = new PictureBox();
            NonSave = new CheckBox();
            CoordChangeButton = new Button();
            CoordCheck = new CheckBox();
            TargetMonLabel = new Label();
            PokemonCombo = new ComboBox();
            FormCombo = new ComboBox();
            RateBox = new TextBox();
            CoordNum = new NumericUpDown();
            PreSavedLabel = new Label();
            LastSaveLabel = new Label();
            LastSavedBox = new TextBox();
            PreSaveBox = new TextBox();
            DelayLabel = new Label();
            DelayNum = new NumericUpDown();
            UptimeLabel = new Label();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox2).BeginInit();
            ((System.ComponentModel.ISupportInitialize)TeleportIndex).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox3).BeginInit();
            ((System.ComponentModel.ISupportInitialize)CoordNum).BeginInit();
            ((System.ComponentModel.ISupportInitialize)DelayNum).BeginInit();
            SuspendLayout();
            // 
            // StopConditions
            // 
            StopConditions.Location = new Point(164, 632);
            StopConditions.Name = "StopConditions";
            StopConditions.Size = new Size(106, 23);
            StopConditions.TabIndex = 0;
            StopConditions.Text = "StopConditions";
            StopConditions.UseVisualStyleBackColor = true;
            StopConditions.Click += StopConditionsButton_Click;
            // 
            // Language
            // 
            Language.FormattingEnabled = true;
            Language.Location = new Point(98, 632);
            Language.Name = "Language";
            Language.Size = new Size(60, 23);
            Language.TabIndex = 61;
            Language.SelectedIndexChanged += LanguageChanged;
            // 
            // button1
            // 
            button1.Location = new Point(310, 528);
            button1.Name = "button1";
            button1.Size = new Size(75, 23);
            button1.TabIndex = 0;
            button1.Text = "Scan!";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // HardStopButton
            // 
            HardStopButton.Location = new Point(33, 528);
            HardStopButton.Name = "HardStopButton";
            HardStopButton.Size = new Size(65, 23);
            HardStopButton.TabIndex = 47;
            HardStopButton.Text = "HardStop";
            HardStopButton.UseVisualStyleBackColor = true;
            HardStopButton.Click += button2_Click;
            // 
            // checkBox4
            // 
            checkBox4.AutoSize = true;
            checkBox4.Location = new Point(103, 533);
            checkBox4.Name = "checkBox4";
            checkBox4.Size = new Size(50, 19);
            checkBox4.TabIndex = 48;
            checkBox4.Text = "Stop";
            checkBox4.UseVisualStyleBackColor = true;
            // 
            // checkBox5
            // 
            checkBox5.AutoSize = true;
            checkBox5.Location = new Point(163, 533);
            checkBox5.Name = "checkBox5";
            checkBox5.Size = new Size(59, 19);
            checkBox5.TabIndex = 48;
            checkBox5.Text = "Reset?";
            checkBox5.UseVisualStyleBackColor = true;
            // 
            // Item7Label
            // 
            Item7Label.AutoSize = true;
            Item7Label.Location = new Point(33, 602);
            Item7Label.Name = "Item7Label";
            Item7Label.Size = new Size(49, 15);
            Item7Label.TabIndex = 19;
            Item7Label.Text = "Coord X";
            // 
            // XCoord
            // 
            XCoord.BackColor = SystemColors.Control;
            XCoord.Location = new Point(94, 599);
            XCoord.Name = "XCoord";
            XCoord.Size = new Size(49, 23);
            XCoord.TabIndex = 20;
            XCoord.TextAlign = HorizontalAlignment.Center;
            // 
            // Item8Label
            // 
            Item8Label.AutoSize = true;
            Item8Label.Location = new Point(147, 602);
            Item8Label.Name = "Item8Label";
            Item8Label.Size = new Size(49, 15);
            Item8Label.TabIndex = 73;
            Item8Label.Text = "Coord Y";
            // 
            // YCoord
            // 
            YCoord.BackColor = SystemColors.Control;
            YCoord.Location = new Point(202, 599);
            YCoord.Name = "YCoord";
            YCoord.Size = new Size(49, 23);
            YCoord.TabIndex = 72;
            YCoord.TextAlign = HorizontalAlignment.Center;
            // 
            // Item9Label
            // 
            Item9Label.AutoSize = true;
            Item9Label.Location = new Point(256, 602);
            Item9Label.Name = "Item9Label";
            Item9Label.Size = new Size(49, 15);
            Item9Label.TabIndex = 75;
            Item9Label.Text = "Coord Z";
            // 
            // ZCoord
            // 
            ZCoord.BackColor = SystemColors.Control;
            ZCoord.Location = new Point(314, 599);
            ZCoord.Name = "ZCoord";
            ZCoord.Size = new Size(49, 23);
            ZCoord.TabIndex = 74;
            ZCoord.TextAlign = HorizontalAlignment.Center;
            // 
            // pictureBox1
            // 
            pictureBox1.Location = new Point(246, 37);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(69, 57);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 1;
            pictureBox1.TabStop = false;
            // 
            // textBox1
            // 
            textBox1.Font = new Font("Yu Gothic UI", 14.25F);
            textBox1.Location = new Point(33, 231);
            textBox1.Multiline = true;
            textBox1.Name = "textBox1";
            textBox1.ReadOnly = true;
            textBox1.Size = new Size(363, 284);
            textBox1.TabIndex = 2;
            textBox1.TextAlign = HorizontalAlignment.Center;
            // 
            // pictureBox2
            // 
            pictureBox2.Location = new Point(33, 37);
            pictureBox2.Margin = new Padding(3, 4, 3, 4);
            pictureBox2.Name = "pictureBox2";
            pictureBox2.Size = new Size(195, 187);
            pictureBox2.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox2.TabIndex = 3;
            pictureBox2.TabStop = false;
            // 
            // TeleportIndex
            // 
            TeleportIndex.Enabled = false;
            TeleportIndex.Location = new Point(201, 666);
            TeleportIndex.Name = "TeleportIndex";
            TeleportIndex.Size = new Size(36, 23);
            TeleportIndex.TabIndex = 78;
            // 
            // TeleportLabel
            // 
            TeleportLabel.AutoSize = true;
            TeleportLabel.Location = new Point(114, 670);
            TeleportLabel.Name = "TeleportLabel";
            TeleportLabel.Size = new Size(81, 15);
            TeleportLabel.TabIndex = 79;
            TeleportLabel.Text = "Teleport Index";
            // 
            // TeleportButton
            // 
            TeleportButton.Enabled = false;
            TeleportButton.Location = new Point(33, 666);
            TeleportButton.Name = "TeleportButton";
            TeleportButton.Size = new Size(75, 23);
            TeleportButton.TabIndex = 82;
            TeleportButton.Text = "Teleport";
            TeleportButton.Click += TeleportButton_Click;
            // 
            // LanguageLabel
            // 
            LanguageLabel.AutoSize = true;
            LanguageLabel.Location = new Point(33, 634);
            LanguageLabel.Name = "LanguageLabel";
            LanguageLabel.Size = new Size(59, 15);
            LanguageLabel.TabIndex = 83;
            LanguageLabel.Text = "Language";
            // 
            // ReadCoordButton
            // 
            ReadCoordButton.Location = new Point(33, 565);
            ReadCoordButton.Name = "ReadCoordButton";
            ReadCoordButton.Size = new Size(90, 23);
            ReadCoordButton.TabIndex = 84;
            ReadCoordButton.Text = "Read Coords";
            ReadCoordButton.Click += ReadCoordButton_Click;
            // 
            // TeleportCoordButton
            // 
            TeleportCoordButton.Location = new Point(129, 565);
            TeleportCoordButton.Name = "TeleportCoordButton";
            TeleportCoordButton.Size = new Size(112, 23);
            TeleportCoordButton.TabIndex = 85;
            TeleportCoordButton.Text = "Teleport To Coords";
            TeleportCoordButton.Click += TeleportCoordButton_Click;
            // 
            // pictureBox3
            // 
            pictureBox3.Location = new Point(246, 112);
            pictureBox3.Margin = new Padding(3, 4, 3, 4);
            pictureBox3.Name = "pictureBox3";
            pictureBox3.Size = new Size(96, 112);
            pictureBox3.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox3.TabIndex = 86;
            pictureBox3.TabStop = false;
            // 
            // NonSave
            // 
            NonSave.AutoSize = true;
            NonSave.Location = new Point(228, 533);
            NonSave.Name = "NonSave";
            NonSave.Size = new Size(76, 19);
            NonSave.TabIndex = 87;
            NonSave.Text = "Non Save";
            NonSave.UseVisualStyleBackColor = true;
            // 
            // CoordChangeButton
            // 
            CoordChangeButton.Location = new Point(247, 565);
            CoordChangeButton.Name = "CoordChangeButton";
            CoordChangeButton.Size = new Size(112, 23);
            CoordChangeButton.TabIndex = 88;
            CoordChangeButton.Text = "Change Coords";
            CoordChangeButton.Click += CoordChangeButton_Click;
            // 
            // CoordCheck
            // 
            CoordCheck.AutoSize = true;
            CoordCheck.Location = new Point(276, 634);
            CoordCheck.Name = "CoordCheck";
            CoordCheck.Size = new Size(143, 19);
            CoordCheck.TabIndex = 89;
            CoordCheck.Text = "Change Trainer coords";
            CoordCheck.UseVisualStyleBackColor = true;
            // 
            // TargetMonLabel
            // 
            TargetMonLabel.AutoSize = true;
            TargetMonLabel.Location = new Point(33, 706);
            TargetMonLabel.Name = "TargetMonLabel";
            TargetMonLabel.Size = new Size(92, 15);
            TargetMonLabel.TabIndex = 90;
            TargetMonLabel.Text = "Target Pokemon";
            // 
            // PokemonCombo
            // 
            PokemonCombo.FormattingEnabled = true;
            PokemonCombo.Location = new Point(131, 703);
            PokemonCombo.Name = "PokemonCombo";
            PokemonCombo.Size = new Size(113, 23);
            PokemonCombo.TabIndex = 91;
            PokemonCombo.SelectedIndexChanged += PokemonCombo_SelectedIndexChanged;
            // 
            // FormCombo
            // 
            FormCombo.FormattingEnabled = true;
            FormCombo.Location = new Point(250, 703);
            FormCombo.Name = "FormCombo";
            FormCombo.Size = new Size(60, 23);
            FormCombo.TabIndex = 92;
            FormCombo.Visible = false;
            // 
            // RateBox
            // 
            RateBox.Font = new Font("Yu Gothic UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 128);
            RateBox.Location = new Point(327, 662);
            RateBox.Multiline = true;
            RateBox.Name = "RateBox";
            RateBox.ReadOnly = true;
            RateBox.Size = new Size(88, 64);
            RateBox.TabIndex = 93;
            RateBox.TextAlign = HorizontalAlignment.Center;
            // 
            // CoordNum
            // 
            CoordNum.Location = new Point(365, 567);
            CoordNum.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            CoordNum.Name = "CoordNum";
            CoordNum.Size = new Size(50, 23);
            CoordNum.TabIndex = 94;
            CoordNum.TextAlign = HorizontalAlignment.Center;
            // 
            // PreSavedLabel
            // 
            PreSavedLabel.AutoSize = true;
            PreSavedLabel.Location = new Point(33, 735);
            PreSavedLabel.Name = "PreSavedLabel";
            PreSavedLabel.Size = new Size(86, 15);
            PreSavedLabel.TabIndex = 95;
            PreSavedLabel.Text = "Previous Saved";
            // 
            // LastSaveLabel
            // 
            LastSaveLabel.AutoSize = true;
            LastSaveLabel.Location = new Point(33, 764);
            LastSaveLabel.Name = "LastSaveLabel";
            LastSaveLabel.Size = new Size(62, 15);
            LastSaveLabel.TabIndex = 96;
            LastSaveLabel.Text = "Last Saved";
            // 
            // LastSavedBox
            // 
            LastSavedBox.Location = new Point(131, 761);
            LastSavedBox.Name = "LastSavedBox";
            LastSavedBox.ReadOnly = true;
            LastSavedBox.Size = new Size(179, 23);
            LastSavedBox.TabIndex = 97;
            LastSavedBox.TextAlign = HorizontalAlignment.Center;
            // 
            // PreSaveBox
            // 
            PreSaveBox.Location = new Point(131, 732);
            PreSaveBox.Name = "PreSaveBox";
            PreSaveBox.ReadOnly = true;
            PreSaveBox.Size = new Size(179, 23);
            PreSaveBox.TabIndex = 98;
            PreSaveBox.TextAlign = HorizontalAlignment.Center;
            // 
            // DelayLabel
            // 
            DelayLabel.AutoSize = true;
            DelayLabel.Location = new Point(321, 735);
            DelayLabel.Name = "DelayLabel";
            DelayLabel.Size = new Size(94, 15);
            DelayLabel.TabIndex = 99;
            DelayLabel.Text = "WaitSec for Save";
            // 
            // DelayNum
            // 
            DelayNum.Increment = new decimal(new int[] { 500, 0, 0, 0 });
            DelayNum.Location = new Point(327, 761);
            DelayNum.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
            DelayNum.Name = "DelayNum";
            DelayNum.Size = new Size(88, 23);
            DelayNum.TabIndex = 100;
            DelayNum.TextAlign = HorizontalAlignment.Center;
            // 
            // UptimeLabel
            // 
            UptimeLabel.AutoSize = true;
            UptimeLabel.Location = new Point(33, 795);
            UptimeLabel.Name = "UptimeLabel";
            UptimeLabel.Size = new Size(48, 15);
            UptimeLabel.TabIndex = 101;
            UptimeLabel.Text = "Uptime:";
            // 
            // StaticViewer
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(427, 819);
            Controls.Add(UptimeLabel);
            Controls.Add(DelayNum);
            Controls.Add(DelayLabel);
            Controls.Add(PreSaveBox);
            Controls.Add(LastSavedBox);
            Controls.Add(LastSaveLabel);
            Controls.Add(PreSavedLabel);
            Controls.Add(CoordNum);
            Controls.Add(RateBox);
            Controls.Add(FormCombo);
            Controls.Add(PokemonCombo);
            Controls.Add(TargetMonLabel);
            Controls.Add(CoordCheck);
            Controls.Add(CoordChangeButton);
            Controls.Add(NonSave);
            Controls.Add(pictureBox3);
            Controls.Add(TeleportCoordButton);
            Controls.Add(ReadCoordButton);
            Controls.Add(LanguageLabel);
            Controls.Add(TeleportLabel);
            Controls.Add(TeleportIndex);
            Controls.Add(Language);
            Controls.Add(pictureBox2);
            Controls.Add(textBox1);
            Controls.Add(pictureBox1);
            Controls.Add(ZCoord);
            Controls.Add(Item9Label);
            Controls.Add(YCoord);
            Controls.Add(Item8Label);
            Controls.Add(XCoord);
            Controls.Add(Item7Label);
            Controls.Add(checkBox5);
            Controls.Add(checkBox4);
            Controls.Add(HardStopButton);
            Controls.Add(button1);
            Controls.Add(StopConditions);
            Controls.Add(TeleportButton);
            Name = "StaticViewer";
            StartPosition = FormStartPosition.CenterParent;
            Text = "PokeViewer.NET - StaticViewerSV";
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox2).EndInit();
            ((System.ComponentModel.ISupportInitialize)TeleportIndex).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox3).EndInit();
            ((System.ComponentModel.ISupportInitialize)CoordNum).EndInit();
            ((System.ComponentModel.ISupportInitialize)DelayNum).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
        #endregion

        private Button button1;
        private Button HardStopButton;
        private CheckBox checkBox4;
        private CheckBox checkBox5;
        public Label Item7Label;
        public TextBox XCoord;
        public Label Item8Label;
        public TextBox YCoord;
        public Label Item9Label;
        public TextBox ZCoord;
        private PictureBox pictureBox1;
        private TextBox textBox1;
        private PictureBox pictureBox2;
        private ComboBox Language;
        private Button StopConditions;
        private NumericUpDown TeleportIndex;
        private Label TeleportLabel;
        private Button TeleportButton;
        private Label LanguageLabel;
        private Button ReadCoordButton;
        private Button TeleportCoordButton;
        private PictureBox pictureBox3;
        private CheckBox NonSave;
        private Button CoordChangeButton;
        private CheckBox CoordCheck;
        private Label TargetMonLabel;
        private ComboBox PokemonCombo;
        private ComboBox FormCombo;
        private TextBox RateBox;
        private NumericUpDown CoordNum;
        private Label PreSavedLabel;
        private Label LastSaveLabel;
        private TextBox LastSavedBox;
        private TextBox PreSaveBox;
        private Label DelayLabel;
        private NumericUpDown DelayNum;
        private Label UptimeLabel;
    }
}