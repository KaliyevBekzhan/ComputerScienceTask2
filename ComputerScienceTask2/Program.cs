using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ComputerScienceTask2
{
    class Payment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int Amount { get; set; }
        public int ProcessingTime { get; set; }
    }

    class Program
    {
        private static List<Payment> successfulPayments = new List<Payment>();
        private static List<Payment> failedPayments = new List<Payment>();
        private static SemaphoreSlim semaphore = new SemaphoreSlim(3);
        private static CancellationTokenSource cts = new CancellationTokenSource();
        private static Random random = new Random();

        static async Task Main()
        {
            List<Payment> payments = GeneratePayments(15);
            List<Task> tasks = new List<Task>();

            // Запуск отдельного потока для обработки нажатия Enter
            Task.Run(() => ReadUserInput());

            foreach (var payment in payments)
            {
                tasks.Add(ProcessPaymentAsync(payment, cts.Token));
            }

            await Task.WhenAll(tasks);
        }

        static void ReadUserInput()
        {
            Console.WriteLine("Нажмите Enter для остановки обработки платежей...");
            while (Console.ReadKey(true).Key != ConsoleKey.Enter) { } // Ждем нажатия Enter
            cts.Cancel();
            Console.WriteLine("Обработка платежей остановлена.");
        }

        static List<Payment> GeneratePayments(int count)
        {
            return Enumerable.Range(1, count)
                .Select(_ => new Payment
                {
                    Amount = random.Next(100, 5001),
                    ProcessingTime = random.Next(1000, 4001)
                })
                .ToList();
        }

        static async Task ProcessPaymentAsync(Payment payment, CancellationToken token)
        {
            try
            {
                await semaphore.WaitAsync(token); // Возможное место исключения

                if (token.IsCancellationRequested)
                {
                    Console.WriteLine($"Платеж {payment.Id} отменен.");
                    return;
                }

                Console.WriteLine($"Обработка платежа {payment.Id} на сумму {payment.Amount}...");
                await Task.Delay(payment.ProcessingTime, token); // Возможная точка отмены

                if (random.Next(100) < 15)
                    throw new Exception("Ошибка при обработке платежа");

                lock (successfulPayments)
                {
                    successfulPayments.Add(payment);
                }

                Console.WriteLine($"Платеж {payment.Id} успешно обработан.");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Обработка платежа {payment.Id} прервана.");
            }
            catch (Exception ex)
            {
                lock (failedPayments)
                {
                    failedPayments.Add(payment);
                }
                Console.WriteLine($"Ошибка обработки платежа {payment.Id}: {ex.Message}");
            }
            finally
            {
                if (semaphore.CurrentCount < 3) // Проверяем, что семафор можно освободить
                    semaphore.Release();
            }
        }
    }
}
