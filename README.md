# HybridCLR结合Addressable进行C#代码热更的示例  
包含自动打包和更新的编辑器脚本以及运行时进入游戏进行热更的逻辑脚本，安卓和Windows已通过测试。  
解决了大部分结合HybridCLR和Addressable使用时出现的问题  
Unity版本:2022.3.17f1c1  
HybridCLR版本:5.1.0  
Addressable版本:1.21.20  
## 使用方法：  
### 安装HybridCLR
进入Unity,点击  
![image](https://github.com/H2ojunjun/HybridCLR_Demo/assets/57307597/7486f0eb-37b9-45aa-8ec3-95fb3e02c7e5)  
再点击：  
![image](https://github.com/H2ojunjun/HybridCLR_Demo/assets/57307597/2d0db85b-7415-499e-87fe-2253e5df94ae)  
安装HybridCLR所需要的il2cpp代码。

### 开启本地Hosting
Demo中Addressable使用的服务器是本地Hosting，需要开启，但可能出现端口号被占用的情况，所以需要打开Addressables Hosting界面进行设置：  
![image](https://github.com/H2ojunjun/HybridCLR_Demo/assets/57307597/af288f21-f2c8-4dea-8e31-488e21971972)  
点击Reset，然后再点击Enable  
![image](https://github.com/H2ojunjun/HybridCLR_Demo/assets/57307597/371edc94-fad2-4851-8811-82a55e05ca54)  

### 开始打包
点击  
![image](https://github.com/H2ojunjun/HybridCLR_Demo/assets/57307597/b378c3aa-4e06-42f5-bbd3-778f921a0dea)  
进行当前平台的打包  
在修改完代码或者资源后，点击  
![image](https://github.com/H2ojunjun/HybridCLR_Demo/assets/57307597/aac10196-312c-428a-9c46-29cd4f8ad650)  
进行热更新  

## 注意事项  
如果想参考本demo，需要注意的地方：  
### 热更Dll之间有依赖
如果有多个热更dll，且这些dll之间如果有依赖关系，需要自行写代码使其按照正确顺序加载：  
如GameLauncher.cs line:50  
![image](https://github.com/H2ojunjun/HybridCLR_Demo/assets/57307597/5a4ad381-498f-4a99-9343-101c43d15ee0)  
这里假设热更的主要程序集名为GamePlay，如果有其他的热更程序集被其依赖，则需要添加到该列表中，如果这些被添加的程序集内部还有依赖，那也需要按依赖顺序写入列表  

## Demo详解
可以看看我在知乎上发的文章:
https://zhuanlan.zhihu.com/p/686662466?

