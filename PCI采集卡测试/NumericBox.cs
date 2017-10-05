using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interactivity;


namespace PCI
{
    /// <summary>
    /// NumericBox功能设计
    /// 只能输入0-9的数字和至多一个小数点；
    ///能够屏蔽通过非正常途径的不正确输入（输入法，粘贴等）；
    ///能够控制小数点后的最大位数，超出位数则无法继续输入；
    ///能够选择当小数点数位数不足时是否补0；
    ///去除开头部分多余的0（为方便处理，当在开头部分输入0时，自动在其后添加一个小数点）；
    ///由于只能输入一个小数点，当在已有的小数点前再次按下小数点，能够跳过小数点；
    /// </summary>
    public class NumericBox : TextBox
    {
        #region Dependency Properties
        /// <summary>
        /// 最大小数点位数
        /// </summary>
        public int MaxFractionDigits
        {
            get { return (int)GetValue(MaxFractionDigitsProperty); }
            set { SetValue(MaxFractionDigitsProperty, value); }
        }
        // Using a DependencyProperty as the backing store for MaxFractionDigits.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MaxFractionDigitsProperty =
            DependencyProperty.Register("MaxFractionDigits", typeof(int), typeof(NumericBox), new PropertyMetadata(2));

        /// <summary>
        /// 不足位数是否补零
        /// </summary>
        public bool IsPadding
        {
            get { return (bool)GetValue(IsPaddingProperty); }
            set { SetValue(IsPaddingProperty, value); }
        }
        // Using a DependencyProperty as the backing store for IsPadding.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsPaddingProperty =
            DependencyProperty.Register("IsPadding", typeof(bool), typeof(NumericBox), new PropertyMetadata(true));

        #endregion

        public NumericBox()
        {
            TextBoxFilterBehavior behavior = new TextBoxFilterBehavior();
            behavior.TextBoxFilterOptions = TextBoxFilterOptions.Numeric | TextBoxFilterOptions.Dot;
            Interaction.GetBehaviors(this).Add(behavior);
            this.TextChanged += new TextChangedEventHandler(NumericBox_TextChanged);
        }

        /// <summary>
        /// 设置Text文本以及光标位置
        /// </summary>
        /// <param name="text"></param>
        private void SetTextAndSelection(string text)
        {
            //保存光标位置
            int selectionIndex = this.SelectionStart;
            this.Text = text;
            //恢复光标位置 系统会自动处理光标位置超出文本长度的情况
            this.SelectionStart = selectionIndex;
        }

        /// <summary>
        /// 去掉开头部分多余的0
        /// </summary>
        private void TrimZeroStart()
        {
            string resultText = this.Text;
            //计算开头部分0的个数
            int zeroCount = 0;
            foreach (char c in this.Text)
            {
                if (c == '0') { zeroCount++; }
                else { break; }
            }

            //当前文本中包含小数点
            if (this.Text.Contains('.'))
            {
                //0后面跟的不是小数点，则删除全部的0
                if (this.Text[zeroCount] != '.')
                {
                    resultText = this.Text.TrimStart('0');
                }
                //否则，保留一个0
                else if (zeroCount > 1)
                {
                    resultText = this.Text.Substring(zeroCount - 1);
                }
            }
            //当前文本中不包含小数点,则保留一个0，并在其后加一个小数点，并将光标设置到小数点前
            else if (zeroCount > 0)
            {
                resultText = "0." + this.Text.TrimStart('0');
                this.SelectionStart = 1;
            }

            SetTextAndSelection(resultText);
        }

        void NumericBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            int decimalIndex = this.Text.IndexOf('.');
            if (decimalIndex >= 0)
            {
                //小数点后的位数
                int lengthAfterDecimal = this.Text.Length - decimalIndex - 1;
                if (lengthAfterDecimal > MaxFractionDigits)
                {
                    SetTextAndSelection(this.Text.Substring(0, this.Text.Length - (lengthAfterDecimal - MaxFractionDigits)));
                }
                else if (IsPadding)
                {
                    SetTextAndSelection(this.Text.PadRight(this.Text.Length + MaxFractionDigits - lengthAfterDecimal, '0'));
                }
            }
            TrimZeroStart();
        }
    }
    /// <summary>
    /// TextBox筛选行为，过滤不需要的按键
    /// </summary>
    public class TextBoxFilterBehavior : Behavior<TextBox>
    {
        private string _prevText = string.Empty;
        public TextBoxFilterBehavior()
        {
        }
        #region Dependency Properties
        /// <summary>
        /// TextBox筛选选项，这里选择的为过滤后剩下的按键
        /// 控制键不参与筛选，可以多选组合
        /// </summary>
        public TextBoxFilterOptions TextBoxFilterOptions
        {
            get { return (TextBoxFilterOptions)GetValue(TextBoxFilterOptionsProperty); }
            set { SetValue(TextBoxFilterOptionsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for TextBoxFilterOptions.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextBoxFilterOptionsProperty =
            DependencyProperty.Register("TextBoxFilterOptions", typeof(TextBoxFilterOptions), typeof(TextBoxFilterBehavior), new PropertyMetadata(TextBoxFilterOptions.None));
        #endregion

        protected override void OnAttached()
        {
            base.OnAttached();
            this.AssociatedObject.KeyDown += new KeyEventHandler(AssociatedObject_KeyDown);
            this.AssociatedObject.TextChanged += new TextChangedEventHandler(AssociatedObject_TextChanged);
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            this.AssociatedObject.KeyDown -= new KeyEventHandler(AssociatedObject_KeyDown);
            this.AssociatedObject.TextChanged -= new TextChangedEventHandler(AssociatedObject_TextChanged);
        }

        #region Events

        /// <summary>
        /// 处理通过其它手段进行的输入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AssociatedObject_TextChanged(object sender, TextChangedEventArgs e)
        {
            //如果符合规则，就保存下来
            if (IsValidText(this.AssociatedObject.Text))
            {
                _prevText = this.AssociatedObject.Text;
            }
            //如果不符合规则，就恢复为之前保存的值
            else
            {
                int selectIndex = this.AssociatedObject.SelectionStart - (this.AssociatedObject.Text.Length - _prevText.Length);
                this.AssociatedObject.Text = _prevText;
                this.AssociatedObject.SelectionStart = selectIndex;
            }

        }

        /// <summary>
        /// 处理按键产生的输入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AssociatedObject_KeyDown(object sender, KeyEventArgs e)
        {
            bool handled = true;
            //不进行过滤
            if (TextBoxFilterOptions == TextBoxFilterOptions.None ||
                KeyboardHelper.IsControlKeys(e.Key))
            {
                handled = false;
            }
            //数字键
            if (handled && TextBoxFilterOptions.ContainsOption(TextBoxFilterOptions.Numeric))
            {
                handled = !KeyboardHelper.IsDigit(e.Key);
            }
            //小数点
            //if (handled && TextBoxFilterOptions.ContainsOption(TextBoxFilterOptions.Dot))
            //{
            //    handled = !(KeyboardHelper.IsDot(e.Key, e.PlatformKeyCode) && !_prevText.Contains("."));
            //    if (KeyboardHelper.IsDot(e.Key, e.PlatformKeyCode) && _prevText.Contains("."))
            //    {
            //        //如果输入位置的下一个就是小数点，则将光标跳到小数点后面
            //        if (this.AssociatedObject.SelectionStart< this.AssociatedObject.Text.Length && _prevText[this.AssociatedObject.SelectionStart] == '.')
            //        {
            //            this.AssociatedObject.SelectionStart++;
            //        }                    
            //    }
            //}
            if (handled && TextBoxFilterOptions.ContainsOption(TextBoxFilterOptions.Dot))
            {
                handled = !(KeyboardHelper.IsDot(e.Key) && !_prevText.Contains("."));
                if (KeyboardHelper.IsDot(e.Key) && _prevText.Contains("."))
                {
                    //如果输入位置的下一个就是小数点，则将光标跳到小数点后面
                    if (this.AssociatedObject.SelectionStart < this.AssociatedObject.Text.Length && _prevText[this.AssociatedObject.SelectionStart] == '.')
                    {
                        this.AssociatedObject.SelectionStart++;
                    }
                }
            }
            //字母
            if (handled && TextBoxFilterOptions.ContainsOption(TextBoxFilterOptions.Character))
            {
                handled = !KeyboardHelper.IsDot(e.Key);
            }
            e.Handled = handled;
        }

        #endregion

        #region Private Methods
        /// <summary>
        /// 判断是否符合规则
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        private bool IsValidChar(char c)
        {
            if (TextBoxFilterOptions == TextBoxFilterOptions.None)
            {
                return true;
            }
            else if (TextBoxFilterOptions.ContainsOption(TextBoxFilterOptions.Numeric) &&
                '0' <= c && c <= '9')
            {
                return true;
            }
            else if (TextBoxFilterOptions.ContainsOption(TextBoxFilterOptions.Dot) &&
                c == '.')
            {
                return true;
            }
            else if (TextBoxFilterOptions.ContainsOption(TextBoxFilterOptions.Character))
            {
                if (('A' <= c && c <= 'Z') || ('a' <= c && c <= 'z'))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 判断文本是否符合规则
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private bool IsValidText(string text)
        {
            //只能有一个小数点
            if (text.IndexOf('.') != text.LastIndexOf('.'))
            {
                return false;
            }
            foreach (char c in text)
            {
                if (!IsValidChar(c))
                {
                    return false;
                }
            }
            return true;
        }
        #endregion
    }
    /// <summary>
    /// TextBox筛选选项
    /// </summary>
    [Flags]
    public enum TextBoxFilterOptions
    {
        /// <summary>
        /// 不采用任何筛选
        /// </summary>
        None = 0,
        /// <summary>
        /// 数字类型不参与筛选
        /// </summary>
        Numeric = 1,
        /// <summary>
        /// 字母类型不参与筛选
        /// </summary>
        Character = 2,
        /// <summary>
        /// 小数点不参与筛选
        /// </summary>
        Dot = 4,
        /// <summary>
        /// 其它类型不参与筛选
        /// </summary>
        Other = 8
    }

    /// <summary>
    /// TextBox筛选选项枚举扩展方法
    /// </summary>
    public static class TextBoxFilterOptionsExtension
    {
        /// <summary>
        /// 在全部的选项中是否包含指定的选项
        /// </summary>
        /// <param name="allOptions">所有的选项</param>
        /// <param name="option">指定的选项</param>
        /// <returns></returns>
        public static bool ContainsOption(this TextBoxFilterOptions allOptions, TextBoxFilterOptions option)
        {
            return (allOptions & option) == option;
        }
    }
    /// <summary>
    /// 键盘操作帮助类
    /// </summary>
    public class KeyboardHelper
    {
        /// <summary>
        /// 键盘上的句号键
        /// </summary>
        public const int OemPeriod = 190;

        #region Fileds

        /// <summary>
        /// 控制键
        /// </summary>
        private static readonly List<Key> _controlKeys = new List<Key>
                                                             {
                                                                 Key.Back,
                                                                 Key.CapsLock,
                                                                 //Key.Ctrl,
                                                                 Key.Down,
                                                                 Key.End,
                                                                 Key.Enter,
                                                                 Key.Escape,
                                                                 Key.Home,
                                                                 Key.Insert,
                                                                 Key.Left,
                                                                 Key.PageDown,
                                                                 Key.PageUp,
                                                                 Key.Right,
                                                                 //Key.Shift,
                                                                 Key.Tab,
                                                                 Key.Up
                                                             };

        #endregion

        /// <summary>
        /// 是否是数字键
        /// </summary>
        /// <param name="key">按键</param>
        /// <returns></returns>
        public static bool IsDigit(Key key)
        {
            bool shiftKey = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            bool retVal;
            //按住shift键后，数字键并不是数字键
            if (key >= Key.D0 && key <= Key.D9 && !shiftKey)
            {
                retVal = true;
            }
            else
            {
                retVal = key >= Key.NumPad0 && key <= Key.NumPad9;
            }
            return retVal;
        }

        /// <summary>
        /// 是否是控制键
        /// </summary>
        /// <param name="key">按键</param>
        /// <returns></returns>
        public static bool IsControlKeys(Key key)
        {
            return _controlKeys.Contains(key);
        }

        /// <summary>
        /// 是否是小数点
        /// Silverlight中无法识别问号左边的那个小数点键
        /// 只能识别小键盘中的小数点
        /// </summary>
        /// <param name="key">按键</param>
        /// <returns></returns>
        public static bool IsDot(Key key)
        {
            bool shiftKey = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            bool flag=false;
            if(key==Key.Decimal)
            {
               flag=true;
            }
            if(key==Key.OemPeriod && !shiftKey)
            {
                flag = true;
            }
            return flag;         
        }

        /// <summary>
        /// 是否是小数点
        /// </summary>
        /// <param name="key">按键</param>
        /// <param name="keyCode">平台相关的按键代码</param>
        /// <returns></returns>
        public static bool IsDot(Key key, int keyCode)
        {
            
            //return IsDot(key) || (key == Key.Unknown && keyCode == OemPeriod);
            return IsDot(key) || (keyCode == OemPeriod);
        }

        /// <summary>
        /// 是否是字母键
        /// </summary>
        /// <param name="key">按键</param>
        /// <returns></returns>
        public static bool IsCharacter(Key key)
        {
            return key >= Key.A && key <= Key.Z;
        }
    }

}
