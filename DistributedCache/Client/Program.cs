using Node.DTO;

namespace Client;

class Program
{
    static async Task Main(string[] args)
    {
        var primaryUrl = new Uri("https://localhost:7275");  
        var backupUrl = new Uri("http://localhost:5001");   // Замените на ваш адрес

        var httpClient = new HttpClient();

        var cacheClient = new DistributedCacheClient(httpClient, primaryUrl, backupUrl);
        var monitor = new CacheClusterMonitor(httpClient, primaryUrl, backupUrl);

        try
        {
            // Создание узла 1
            // Console.WriteLine("Creating node...");
            // var nodeInfo = await cacheClient.CreateNodeAsync("TestContainer", 2);
            // Console.WriteLine($"Node created: {nodeInfo}");

            // Получение всех узлов 1
            // Console.WriteLine("Getting all nodes with data...");
            // var nodesData = await monitor.GetAllNodesWithDataAsync<object>(); // Замените object на свою модель при необходимости
            // Console.WriteLine($"Nodes Data: {nodesData}");
            
            // Установка значения 1
            // Console.WriteLine("Setting value to cache...");
            // await cacheClient.SetAsync("myKey", "Hello, Cluster!", 60);

            // Получение значения1
            // Console.WriteLine("Getting value from cache...");
            // var value = await cacheClient.GetAsync<string>("myKey");
            // Console.WriteLine($"Value from cache: {value}");

            // Удаление узла1
            // Console.WriteLine("Deleting node...");
            // var delResult = await cacheClient.DeleteNodeAsync("node-container-TestContainer-712b7b61-f1fb-4fb1-9b8a-bd9aff8ad43f", force: true);
            // Console.WriteLine($"Node deleted: {delResult}");

            // Получение статуса всех узлов1
            // Console.WriteLine("Getting status of all nodes...");
            // var status = await monitor.GetStatusAllNodesAsync<object>();
            // Console.WriteLine($"Cluster status: {status}");

            // Console.WriteLine("\nПолучение одного узла с ключом \"node-container-TestContainer-09981679-241c-4d87-8a4b-40ab07bbdb33\"...");
            // var node = await monitor.GetNodeWithDataAsync<object>("node-container-TestContainer-09981679-241c-4d87-8a4b-40ab07bbdb33");
            // Console.WriteLine("Данные узла:");
            // Console.WriteLine(node);
            //
            // Console.WriteLine("\nПолучение статуса узла с ключом \"node-container-TestContainer-09981679-241c-4d87-8a4b-40ab07bbdb33\"...");
            // var nodeStatus = await monitor.GetStatusNodeAsync<object>("node-container-TestContainer-09981679-241c-4d87-8a4b-40ab07bbdb33");
            // Console.WriteLine("Статус узла:");
            // Console.WriteLine(nodeStatus);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error occurred: {ex.Message}");
        }
    }
}