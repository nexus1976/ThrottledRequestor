using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ThrottledRequestor
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            const int MAX_REQUESTS = 100;
            const string ENDPOINT_URL = @"https://www.boredapi.com/api/activity";
            const int QUEUE_FREQUENCY = 100;
            const int MAX_REQUESTS_PER_ITERATION = 3;
            const int ITERATION_DURATION = 1000;
            var cts = new CancellationTokenSource();

            // using ConcurrentQueue
            //var queue = new ConcurrentQueue<RequestModel>();
            //var completedRequests = new ConcurrentStack<RequestModel>();
            //var hydrateTaskQueue = Task.Run(() => QueueRequests(MAX_REQUESTS, QUEUE_FREQUENCY, ENDPOINT_URL, queue), cts.Token);
            //var processTaskQueue = Task.Run(() => ProcessRequests(MAX_REQUESTS_PER_ITERATION, ITERATION_DURATION, queue, completedRequests, cts.Token), cts.Token);
            //Task.WaitAll(hydrateTaskQueue, processTaskQueue);

            var queue = new BlockingCollection<RequestModel>();
            var completedRequests = new ConcurrentStack<RequestModel>();
            var hydrateTaskQueue = Task.Run(() => QueueRequests(MAX_REQUESTS, QUEUE_FREQUENCY, ENDPOINT_URL, queue, cts.Token), cts.Token);
            var processTaskQueue = Task.Run(() => ProcessRequests(MAX_REQUESTS_PER_ITERATION, ITERATION_DURATION, queue, completedRequests, cts.Token), cts.Token);
            Task.WaitAll(hydrateTaskQueue);

            Console.WriteLine("Queued all my requests. Taking a nap.");
            Thread.Sleep(30000);
            Console.WriteLine("Nice nap...let's do that hydrateTaskQueue once more.");
            hydrateTaskQueue = Task.Run(() => QueueRequests(MAX_REQUESTS, QUEUE_FREQUENCY, ENDPOINT_URL, queue, cts.Token), cts.Token);
            Task.WaitAll(hydrateTaskQueue);

            Console.WriteLine("");
            Console.WriteLine("Processing Complete");
            Console.ReadLine();            
        }

        public static void QueueRequests(int maxRequests, int queueFrequency, string endpointUrl, BlockingCollection<RequestModel> queue, CancellationToken cancellationToken)
        {
            for (int i = 0; i < maxRequests - 1; i++)
            {
                var requstModel = RequestModel.Create(endpointUrl);
                queue.Add(requstModel, cancellationToken);
                Console.WriteLine($"Thread Id: {Environment.CurrentManagedThreadId} enqueued request # {i}");
                Thread.Sleep(queueFrequency);
            }
        }
        public static void QueueRequests(int maxRequests, int queueFrequency, string endpointUrl, ConcurrentQueue<RequestModel> queue)
        {
            for (int i = 0; i < maxRequests - 1; i++)
            {
                var requestModel = RequestModel.Create(endpointUrl);
                queue.Enqueue(requestModel);
                Console.WriteLine($"Thread Id: {Environment.CurrentManagedThreadId} enqueued request # {i}");
                Thread.Sleep(queueFrequency);
            }
        }

        public static async Task ProcessRequests(int maxRequestsPerIteration, int iterationDuration,
            BlockingCollection<RequestModel> queue, ConcurrentStack<RequestModel> completedRequests, CancellationToken cancellationToken)
        {
            if (queue == null) return;
            if (completedRequests == null) return;

            using HttpClient httpClient = new();
            int counter = 0;
            Stopwatch stopwatch = new();

            while(true)
            {
                var request = queue.Take(cancellationToken);
                if (request != null && !string.IsNullOrWhiteSpace(request.Url))
                {
                    if (!stopwatch.IsRunning)
                    {
                        counter = 0;
                        stopwatch.Reset();
                        stopwatch.Start();
                    }
                    if (counter == maxRequestsPerIteration)
                    {
                        counter = 0;
                        stopwatch.Stop();
                        if (stopwatch.ElapsedMilliseconds < iterationDuration)
                        {
                            int sleepDuration = (int)(iterationDuration - stopwatch.ElapsedMilliseconds);
                            Thread.Sleep(sleepDuration);
                        }
                        stopwatch.Reset();
                        stopwatch.Start();
                    }
                    counter++;
                    using var response = await httpClient.GetAsync(request.Url, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        request.ResponseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        completedRequests.Push(request);
                    }
                    Console.WriteLine($"Thread Id: {Environment.CurrentManagedThreadId} dequeued request at timer mark: {stopwatch.ElapsedMilliseconds} milliseconds");
                }
            }
        }

        public static async Task ProcessRequests(int maxRequestsPerIteration, int iterationDuration,
            ConcurrentQueue<RequestModel> queue, ConcurrentStack<RequestModel> completedRequests, CancellationToken cancellationToken)
        {
            if (queue == null) return;
            if (completedRequests == null) return;

            using HttpClient httpClient = new();
            int counter = 0;
            Stopwatch stopwatch = new();

            while (!queue.IsEmpty)
            {
                if (queue.TryDequeue(out RequestModel? request) && !string.IsNullOrWhiteSpace(request.Url))
                {
                    if (!stopwatch.IsRunning)
                    {
                        counter = 0;
                        stopwatch.Reset();
                        stopwatch.Start();
                    }
                    if (counter >= maxRequestsPerIteration)
                    {
                        counter = 0;
                        stopwatch.Stop();
                        if (stopwatch.ElapsedMilliseconds < iterationDuration)
                        {
                            int sleepDuration = (int)(iterationDuration - stopwatch.ElapsedMilliseconds);
                            Thread.Sleep(sleepDuration);
                        }
                        stopwatch.Reset();
                        stopwatch.Start();
                    }
                    counter++;
                    using var response = await httpClient.GetAsync(request.Url, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        request.ResponseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        completedRequests.Push(request);
                    }
                    Console.WriteLine($"Thread Id: {Environment.CurrentManagedThreadId} dequeued request at timer mark: {stopwatch.ElapsedMilliseconds} milliseconds");
                }
            }
        }
    }
}