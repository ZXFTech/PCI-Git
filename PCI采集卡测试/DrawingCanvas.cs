using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCI
{
    public class DrawingCanvas:Canvas
    {
        TestClass testClass = new TestClass();

        private List<Visual> visuals = new List<Visual>();

        //获取Visual的个数
        protected override int VisualChildrenCount
        {
            get
            {
                return visuals.Count;
            }
        }

        public int visualChildrenCount
        {
            get
            {
                return visuals.Count;
            }
        }

        //获取Visual
        protected override Visual GetVisualChild(int index)
        {
            return visuals[index];
        }

        public Visual getVisualChild(int index)
        {
            return GetVisualChild(index);
        }

        //添加Visual
        public void AddVisual(Visual visual)
        {
            visuals.Add(visual);

            base.AddVisualChild(visual);
            base.AddLogicalChild(visual);
        }

        //删除Visual
        public void RemoveVisual(Visual visual)
        {
            visuals.Remove(visual);

            base.RemoveLogicalChild(visual);
            base.RemoveVisualChild(visual);
        }

        //命中测试
        public DrawingVisual GetVisual(Point point)
        {
            HitTestResult hitResult = VisualTreeHelper.HitTest(this, point);
            return hitResult.VisualHit as DrawingVisual;
        }
    }
}
