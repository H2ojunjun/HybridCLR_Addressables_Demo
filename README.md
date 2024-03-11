# HybridCLR结合Addressable进行C#代码热更的示例  
包含自动打包和更新的编辑器脚本以及运行时进入游戏进行热更的逻辑脚本，安卓和Windows已通过测试。  
解决了大部分结合HybridCLR和Addressable使用时出现的问题  
Unity版本:2022.3.17f1c1  
HybridCLR版本:5.1.0  
Addressable版本:1.21.20  
## 使用方法：  
进入Unity,点击  
![image](https://github.com/H2ojunjun/HybridCLR_Demo/assets/57307597/7486f0eb-37b9-45aa-8ec3-95fb3e02c7e5)  
再点击：  
![image](https://github.com/H2ojunjun/HybridCLR_Demo/assets/57307597/2d0db85b-7415-499e-87fe-2253e5df94ae)  
安装HybridCLR所需要的il2cpp代码。
点击  
![image](https://github.com/H2ojunjun/HybridCLR_Demo/assets/57307597/b378c3aa-4e06-42f5-bbd3-778f921a0dea)  
进行当前平台的打包  
在修改完代码或者资源后，点击  
![image](https://github.com/H2ojunjun/HybridCLR_Demo/assets/57307597/aac10196-312c-428a-9c46-29cd4f8ad650)  
进行热更新  

## 注意事项  
如果想参考本demo，需要注意的地方：  
1.如果有多个热更dll，且这些dll之间如果有依赖关系，需要自行写代码使其按照正确顺序加载：  
如GameLauncher.cs line:50  
![image](https://github.com/H2ojunjun/HybridCLR_Demo/assets/57307597/5a4ad381-498f-4a99-9343-101c43d15ee0)  
这里假设热更的主要程序集名为GamePlay，如果有其他的热更程序集被其依赖，则需要添加到该列表中，如果这些被添加的程序集内部还有依赖，那也需要按依赖顺序写入列表  
2.如果热更代码中使用了attribute:RuntimeInitializeOnLoadMethod，则需要我们自行在游戏主逻辑跑之前反射调用这些被RuntimeInitializeOnLoadMethod标记的函数  
此demo中解决了该问题  
GameLauncher.cs line:55  
![image](https://github.com/H2ojunjun/HybridCLR_Demo/assets/57307597/9a7ce909-c89d-4dd0-9825-102b2090c31d)  
可以全局搜索使用了RuntimeInitializeOnLoadMethod attribute的函数，然后将其程序集名称(大部分IDE都能很方便看到)写在该列表内，程序会自动扫描这些程序集然后执行这些方法  

