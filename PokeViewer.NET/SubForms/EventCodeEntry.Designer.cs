namespace PokeViewer.NET.SubForms
{
    partial class EventCodeEntry
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
            DGV_View = new ItemResultGridView();
            Num = new NumericUpDown();
            RedeemCount = new Label();
            ResetMysteryGifts = new Button();
            RedeemButton = new Button();
            HardStop = new Button();
            GiftCode = new TextBox();
            ClearButton = new Button();
            label1 = new Label();
            OverShoot = new NumericUpDown();
            HasReset = new CheckBox();
            Item = new CheckBox();
            FirstConnect = new CheckBox();
            StopConditions = new Button();
            pictureBox2 = new PictureBox();
            pictureBox3 = new PictureBox();
            pictureBox1 = new PictureBox();
            NonCode = new CheckBox();
            textBox2 = new TextBox();
            LinkAccount = new CheckBox();
            filter = new CheckBox();
            AutoPaste = new Button();
            Repeatable = new CheckBox();
            RateBox = new TextBox();
            MultiGift = new CheckBox();
            PresentLabel = new Label();
            PresentNum = new NumericUpDown();
            ((System.ComponentModel.ISupportInitialize)Num).BeginInit();
            ((System.ComponentModel.ISupportInitialize)OverShoot).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox2).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox3).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)PresentNum).BeginInit();
            SuspendLayout();
            // 
            // DGV_View
            // 
            DGV_View.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            DGV_View.Location = new Point(18, 207);
            DGV_View.Name = "DGV_View";
            DGV_View.Size = new Size(315, 228);
            DGV_View.TabIndex = 97;
            DGV_View.Visible = false;
            // 
            // Num
            // 
            Num.Location = new Point(222, 539);
            Num.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            Num.Name = "Num";
            Num.Size = new Size(50, 23);
            Num.TabIndex = 10;
            Num.TextAlign = HorizontalAlignment.Center;
            Num.Value = new decimal(new int[] { 10, 0, 0, 0 });
            // 
            // RedeemCount
            // 
            RedeemCount.AutoSize = true;
            RedeemCount.Font = new Font("Segoe UI", 9.75F);
            RedeemCount.Location = new Point(99, 541);
            RedeemCount.Name = "RedeemCount";
            RedeemCount.Size = new Size(117, 17);
            RedeemCount.TabIndex = 11;
            RedeemCount.Text = "Redemption Count";
            // 
            // ResetMysteryGifts
            // 
            ResetMysteryGifts.Location = new Point(286, 512);
            ResetMysteryGifts.Name = "ResetMysteryGifts";
            ResetMysteryGifts.Size = new Size(130, 23);
            ResetMysteryGifts.TabIndex = 7;
            ResetMysteryGifts.Text = "ResetMysteryGifts";
            ResetMysteryGifts.UseVisualStyleBackColor = true;
            ResetMysteryGifts.Click += button4_Click;
            // 
            // RedeemButton
            // 
            RedeemButton.Location = new Point(15, 485);
            RedeemButton.Name = "RedeemButton";
            RedeemButton.Size = new Size(98, 23);
            RedeemButton.TabIndex = 0;
            RedeemButton.Text = "Redeem!";
            RedeemButton.UseVisualStyleBackColor = true;
            RedeemButton.Click += RedeemButton_Click;
            // 
            // HardStop
            // 
            HardStop.Location = new Point(18, 539);
            HardStop.Name = "HardStop";
            HardStop.Size = new Size(75, 23);
            HardStop.TabIndex = 5;
            HardStop.Text = "HardStop";
            HardStop.UseVisualStyleBackColor = true;
            HardStop.Click += button2_Click;
            // 
            // GiftCode
            // 
            GiftCode.Location = new Point(18, 441);
            GiftCode.MaxLength = 16;
            GiftCode.Multiline = true;
            GiftCode.Name = "GiftCode";
            GiftCode.Size = new Size(233, 36);
            GiftCode.TabIndex = 1;
            // 
            // ClearButton
            // 
            ClearButton.Location = new Point(164, 485);
            ClearButton.Name = "ClearButton";
            ClearButton.Size = new Size(98, 23);
            ClearButton.TabIndex = 2;
            ClearButton.Text = "Clear Text";
            ClearButton.UseVisualStyleBackColor = true;
            ClearButton.Click += ClearButton_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 15F);
            label1.Location = new Point(119, 480);
            label1.Name = "label1";
            label1.Size = new Size(39, 28);
            label1.TabIndex = 3;
            label1.Text = "🎁";
            // 
            // OverShoot
            // 
            OverShoot.Increment = new decimal(new int[] { 100, 0, 0, 0 });
            OverShoot.Location = new Point(278, 485);
            OverShoot.Maximum = new decimal(new int[] { 2500, 0, 0, 0 });
            OverShoot.Name = "OverShoot";
            OverShoot.Size = new Size(95, 23);
            OverShoot.TabIndex = 4;
            OverShoot.TextAlign = HorizontalAlignment.Center;
            // 
            // HasReset
            // 
            HasReset.AutoSize = true;
            HasReset.Location = new Point(18, 514);
            HasReset.Name = "HasReset";
            HasReset.Size = new Size(82, 19);
            HasReset.TabIndex = 9;
            HasReset.Text = "Has Reset?";
            HasReset.UseVisualStyleBackColor = true;
            // 
            // Item
            // 
            Item.AutoSize = true;
            Item.Location = new Point(106, 514);
            Item.Name = "Item";
            Item.Size = new Size(76, 19);
            Item.TabIndex = 8;
            Item.Text = "Item Gift?";
            Item.UseVisualStyleBackColor = true;
            Item.CheckedChanged += Item_CheckedChanged;
            // 
            // FirstConnect
            // 
            FirstConnect.AutoSize = true;
            FirstConnect.Location = new Point(188, 512);
            FirstConnect.Name = "FirstConnect";
            FirstConnect.Size = new Size(92, 19);
            FirstConnect.TabIndex = 6;
            FirstConnect.Text = "FirstConnect";
            FirstConnect.UseVisualStyleBackColor = true;
            // 
            // StopConditions
            // 
            StopConditions.Location = new Point(286, 541);
            StopConditions.Name = "StopConditions";
            StopConditions.Size = new Size(130, 23);
            StopConditions.TabIndex = 12;
            StopConditions.Text = "StopConditions";
            StopConditions.UseVisualStyleBackColor = true;
            StopConditions.Click += StopConditions_Click;
            // 
            // pictureBox2
            // 
            pictureBox2.Location = new Point(18, 13);
            pictureBox2.Margin = new Padding(3, 4, 3, 4);
            pictureBox2.Name = "pictureBox2";
            pictureBox2.Size = new Size(195, 187);
            pictureBox2.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox2.TabIndex = 13;
            pictureBox2.TabStop = false;
            // 
            // pictureBox3
            // 
            pictureBox3.Location = new Point(237, 88);
            pictureBox3.Margin = new Padding(3, 4, 3, 4);
            pictureBox3.Name = "pictureBox3";
            pictureBox3.Size = new Size(96, 112);
            pictureBox3.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox3.TabIndex = 87;
            pictureBox3.TabStop = false;
            // 
            // pictureBox1
            // 
            pictureBox1.Location = new Point(237, 13);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(69, 57);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 88;
            pictureBox1.TabStop = false;
            // 
            // NonCode
            // 
            NonCode.AutoSize = true;
            NonCode.Location = new Point(318, 576);
            NonCode.Name = "NonCode";
            NonCode.Size = new Size(88, 19);
            NonCode.TabIndex = 89;
            NonCode.Text = "Online Gift?";
            NonCode.UseVisualStyleBackColor = true;
            NonCode.CheckedChanged += NonCode_CheckedChanged;
            // 
            // textBox2
            // 
            textBox2.Font = new Font("Yu Gothic UI", 14.25F);
            textBox2.Location = new Point(18, 207);
            textBox2.Multiline = true;
            textBox2.Name = "textBox2";
            textBox2.ReadOnly = true;
            textBox2.Size = new Size(315, 228);
            textBox2.TabIndex = 90;
            textBox2.TextAlign = HorizontalAlignment.Center;
            // 
            // LinkAccount
            // 
            LinkAccount.AutoSize = true;
            LinkAccount.Location = new Point(110, 576);
            LinkAccount.Name = "LinkAccount";
            LinkAccount.Size = new Size(101, 19);
            LinkAccount.TabIndex = 93;
            LinkAccount.Text = "Nintendo Link";
            LinkAccount.UseVisualStyleBackColor = true;
            // 
            // filter
            // 
            filter.AutoSize = true;
            filter.Location = new Point(18, 576);
            filter.Name = "filter";
            filter.Size = new Size(86, 19);
            filter.TabIndex = 94;
            filter.Text = "Filter Mode";
            filter.UseVisualStyleBackColor = true;
            filter.CheckedChanged += Filter_CheckedChanged;
            // 
            // AutoPaste
            // 
            AutoPaste.Location = new Point(278, 454);
            AutoPaste.Name = "AutoPaste";
            AutoPaste.Size = new Size(75, 23);
            AutoPaste.TabIndex = 95;
            AutoPaste.Text = "AutoPaste";
            AutoPaste.UseVisualStyleBackColor = true;
            AutoPaste.Click += AutoPaste_Click;
            // 
            // Repeatable
            // 
            Repeatable.AutoSize = true;
            Repeatable.Location = new Point(217, 576);
            Repeatable.Name = "Repeatable";
            Repeatable.Size = new Size(95, 19);
            Repeatable.TabIndex = 96;
            Repeatable.Text = "Is Repeatable";
            Repeatable.UseVisualStyleBackColor = true;
            // 
            // RateBox
            // 
            RateBox.Font = new Font("Yu Gothic UI", 7.8F, FontStyle.Regular, GraphicsUnit.Point, 128);
            RateBox.Location = new Point(339, 342);
            RateBox.Multiline = true;
            RateBox.Name = "RateBox";
            RateBox.ReadOnly = true;
            RateBox.Size = new Size(86, 93);
            RateBox.TabIndex = 98;
            RateBox.TextAlign = HorizontalAlignment.Center;
            // 
            // MultiGift
            // 
            MultiGift.AutoSize = true;
            MultiGift.Location = new Point(318, 601);
            MultiGift.Name = "MultiGift";
            MultiGift.Size = new Size(81, 19);
            MultiGift.TabIndex = 99;
            MultiGift.Text = "Multi Gift?";
            MultiGift.UseVisualStyleBackColor = true;
            MultiGift.Visible = false;
            // 
            // PresentLabel
            // 
            PresentLabel.AutoSize = true;
            PresentLabel.Location = new Point(15, 605);
            PresentLabel.Name = "PresentLabel";
            PresentLabel.Size = new Size(78, 15);
            PresentLabel.TabIndex = 100;
            PresentLabel.Text = "Present Index";
            // 
            // PresentNum
            // 
            PresentNum.Location = new Point(99, 603);
            PresentNum.Name = "PresentNum";
            PresentNum.Size = new Size(43, 23);
            PresentNum.TabIndex = 101;
            PresentNum.TextAlign = HorizontalAlignment.Center;
            // 
            // EventCodeEntry
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(437, 639);
            Controls.Add(PresentNum);
            Controls.Add(PresentLabel);
            Controls.Add(MultiGift);
            Controls.Add(RateBox);
            Controls.Add(Repeatable);
            Controls.Add(AutoPaste);
            Controls.Add(filter);
            Controls.Add(LinkAccount);
            Controls.Add(textBox2);
            Controls.Add(NonCode);
            Controls.Add(pictureBox1);
            Controls.Add(pictureBox3);
            Controls.Add(pictureBox2);
            Controls.Add(StopConditions);
            Controls.Add(ClearButton);
            Controls.Add(GiftCode);
            Controls.Add(RedeemButton);
            Controls.Add(label1);
            Controls.Add(HasReset);
            Controls.Add(OverShoot);
            Controls.Add(HardStop);
            Controls.Add(FirstConnect);
            Controls.Add(ResetMysteryGifts);
            Controls.Add(Item);
            Controls.Add(Num);
            Controls.Add(RedeemCount);
            Controls.Add(DGV_View);
            MaximizeBox = false;
            Name = "EventCodeEntry";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Code Entry";
            ((System.ComponentModel.ISupportInitialize)Num).EndInit();
            ((System.ComponentModel.ISupportInitialize)OverShoot).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox2).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox3).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ((System.ComponentModel.ISupportInitialize)PresentNum).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
        #endregion

        private Button RedeemButton;
        private TextBox GiftCode;
        private Button ClearButton;
        private Label label1;
        private NumericUpDown OverShoot;
        private CheckBox HasReset;
        private CheckBox Item;
        private CheckBox FirstConnect;
        private Button HardStop;
        private Button ResetMysteryGifts;
        private NumericUpDown Num;
        private Label RedeemCount;
        private Button StopConditions;
        private PictureBox pictureBox2;
        private PictureBox pictureBox3;
        private PictureBox pictureBox1;
        private CheckBox NonCode;
        private TextBox textBox2;
        private CheckBox LinkAccount;
        private CheckBox filter;
        private Button AutoPaste;
        private CheckBox Repeatable;
        private ItemResultGridView DGV_View;
        private TextBox RateBox;
        private CheckBox MultiGift;
        private Label PresentLabel;
        private NumericUpDown PresentNum;
    }
}