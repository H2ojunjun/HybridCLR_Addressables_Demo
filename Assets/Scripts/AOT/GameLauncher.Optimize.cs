using UnityEngine;

namespace AOT
{
    /// <summary>
    /// 优化HybridCLR相关
    /// 频繁使用的泛型在此类中显示调用，然后走IL2CPP泛型共享机制，不走HybridCLR解释执行,性能更优
    /// 如果managed strip level开得高，可能会导致有的程序集即使在link文件中保留依然被裁掉，我们需要在代码中显示调用这些程序集中的某个函数
    /// </summary>
    public partial class GameLauncher
    {
        private void OptimizeHybridCLR()
        {
            OptimizeGenericType();
            CallMethodInAssemblyExplicitly();
        }

        //显示访问频繁使用的泛型
        private void OptimizeGenericType()
        {
            
        }

        //显示调用其他程序集防止裁剪(目前还未发现在不访问某程序集任何代码，link.xml中有保留，程序集被裁剪的情况)
        private void CallMethodInAssemblyExplicitly()
        {
            
        }
    }
}