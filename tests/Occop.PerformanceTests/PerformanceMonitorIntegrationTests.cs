using FluentAssertions;
using Occop.Core.Performance;
using Xunit;

namespace Occop.PerformanceTests
{
    /// <summary>
    /// 性能监控器集成测试
    /// Performance monitor integration tests
    /// </summary>
    public class PerformanceMonitorIntegrationTests
    {
        [Fact]
        public void PerformanceMonitor_ShouldTrackOperations()
        {
            // Arrange
            var monitor = new PerformanceMonitor();

            // Act
            using (var timer = monitor.BeginOperation("TestOperation", "Integration"))
            {
                Thread.Sleep(100);
            }

            // Assert
            var stats = monitor.GetStatistics("TestOperation");
            stats.Should().NotBeNull();
            stats!.TotalExecutions.Should().Be(1);
            stats.AverageDurationMs.Should().BeGreaterOrEqualTo(100);
        }

        [Fact]
        public void PerformanceMonitor_ShouldDetectDegradation()
        {
            // Arrange
            var monitor = new PerformanceMonitor();

            // 记录一些正常的操作
            for (int i = 0; i < 10; i++)
            {
                monitor.RecordOperation("SlowOperation", 100, true);
            }

            // 记录一些变慢的操作
            for (int i = 0; i < 10; i++)
            {
                monitor.RecordOperation("SlowOperation", 200, true);
            }

            // Act
            var degraded = monitor.DetectDegradation("SlowOperation", 20.0);

            // Assert
            degraded.Should().BeTrue();
        }

        [Fact]
        public void PerformanceMonitor_ShouldTrackMemory()
        {
            // Arrange
            var monitor = new PerformanceMonitor();

            // Act
            monitor.RecordMemoryUsage();
            var snapshot = monitor.GetMemorySnapshot();

            // Assert
            snapshot.Should().NotBeNull();
            snapshot.WorkingSetBytes.Should().BeGreaterThan(0);
            snapshot.ManagedHeapBytes.Should().BeGreaterThan(0);
        }

        [Fact]
        public void PerformanceMonitor_ConcurrentOperations_ShouldBeThreadSafe()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            const int operationCount = 1000;

            // Act
            Parallel.For(0, operationCount, i =>
            {
                using var timer = monitor.BeginOperation($"Operation_{i % 10}", "Concurrent");
                Thread.Sleep(1);
            });

            // Assert
            var allStats = monitor.GetAllStatistics();
            var totalExecutions = allStats.Values.Sum(s => s.TotalExecutions);
            totalExecutions.Should().Be(operationCount);
        }

        [Fact]
        public void MemoryAnalyzer_ShouldAnalyzeSnapshot()
        {
            // Arrange
            var analyzer = new MemoryAnalyzer();
            var snapshot = new MemorySnapshot
            {
                Timestamp = DateTime.UtcNow,
                WorkingSetBytes = 600 * 1024 * 1024, // 600 MB - should trigger warning
                ManagedHeapBytes = 250 * 1024 * 1024, // 250 MB - should trigger warning
                Gen0CollectionCount = 100,
                Gen1CollectionCount = 50,
                Gen2CollectionCount = 20
            };

            // Act
            var result = analyzer.Analyze(snapshot);

            // Assert
            result.Should().NotBeNull();
            result.Issues.Should().NotBeEmpty();
            result.HasWarnings.Should().BeTrue();
        }

        [Fact]
        public void MemoryAnalyzer_ShouldDetectMemoryLeak()
        {
            // Arrange
            var analyzer = new MemoryAnalyzer();
            var snapshots = new List<MemorySnapshot>();
            var baseTime = DateTime.UtcNow;

            // 模拟内存持续增长
            for (int i = 0; i < 10; i++)
            {
                snapshots.Add(new MemorySnapshot
                {
                    Timestamp = baseTime.AddMinutes(i),
                    WorkingSetBytes = (100 + i * 20) * 1024 * 1024,
                    ManagedHeapBytes = (50 + i * 10) * 1024 * 1024,
                    Gen0CollectionCount = 10 + i,
                    Gen1CollectionCount = 5,
                    Gen2CollectionCount = 2
                });
            }

            // Act
            var leaked = analyzer.DetectMemoryLeak(snapshots, 10.0);

            // Assert
            leaked.Should().BeTrue();
        }

        [Fact]
        public void MemoryAnalyzer_ShouldGenerateTrendReport()
        {
            // Arrange
            var analyzer = new MemoryAnalyzer();
            var snapshots = new List<MemorySnapshot>();
            var baseTime = DateTime.UtcNow;

            for (int i = 0; i < 5; i++)
            {
                snapshots.Add(new MemorySnapshot
                {
                    Timestamp = baseTime.AddMinutes(i),
                    WorkingSetBytes = (100 + i * 10) * 1024 * 1024,
                    ManagedHeapBytes = (50 + i * 5) * 1024 * 1024,
                    Gen0CollectionCount = 10 + i,
                    Gen1CollectionCount = 5,
                    Gen2CollectionCount = 2
                });
            }

            // Act
            var report = analyzer.GenerateTrendReport(snapshots);

            // Assert
            report.Should().NotBeNull();
            report.SnapshotCount.Should().Be(5);
            report.AverageWorkingSetMB.Should().BeGreaterThan(0);
            report.WorkingSetTrendMBPerHour.Should().BeGreaterThan(0);
        }

        [Fact]
        public void PerformanceAlertManager_ShouldRaiseAlerts()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var analyzer = new MemoryAnalyzer();
            var config = new PerformanceAlertConfig
            {
                DegradationThreshold = 20.0,
                HighMemoryThresholdMB = 100.0,
                CheckIntervalSeconds = 1
            };

            var alertManager = new PerformanceAlertManager(monitor, analyzer, config);
            PerformanceAlertEventArgs? capturedAlert = null;
            alertManager.AlertRaised += (sender, args) => capturedAlert = args;

            // 记录一些导致降级的操作
            for (int i = 0; i < 10; i++)
            {
                monitor.RecordOperation("TestOp", 100, true);
            }
            for (int i = 0; i < 10; i++)
            {
                monitor.RecordOperation("TestOp", 200, true);
            }

            // Act
            alertManager.CheckNow();

            // Assert
            capturedAlert.Should().NotBeNull();
            capturedAlert!.AlertType.Should().Be(PerformanceAlertType.Degradation);
        }

        [Fact]
        public async Task PerformanceReportGenerator_ShouldGenerateReports()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var analyzer = new MemoryAnalyzer();

            // 记录一些操作
            for (int i = 0; i < 5; i++)
            {
                using var timer = monitor.BeginOperation($"Operation{i}", "Test");
                Thread.Sleep(10);
            }

            var generator = new PerformanceReportGenerator(monitor, analyzer);

            // Act
            var markdownReport = generator.GenerateReport(Reports.ReportFormat.Markdown);
            var textReport = generator.GenerateReport(Reports.ReportFormat.Text);
            var htmlReport = generator.GenerateReport(Reports.ReportFormat.Html);
            var jsonReport = generator.GenerateReport(Reports.ReportFormat.Json);

            // Assert
            markdownReport.Should().NotBeNullOrEmpty();
            markdownReport.Should().Contain("# Performance Report");

            textReport.Should().NotBeNullOrEmpty();
            textReport.Should().Contain("PERFORMANCE REPORT");

            htmlReport.Should().NotBeNullOrEmpty();
            htmlReport.Should().Contain("<html>");

            jsonReport.Should().NotBeNullOrEmpty();
            jsonReport.Should().Contain("generatedAt");

            // 测试保存到文件
            var tempFile = Path.GetTempFileName();
            try
            {
                await generator.SaveReportAsync(tempFile, Reports.ReportFormat.Markdown);
                var savedContent = await File.ReadAllTextAsync(tempFile);
                savedContent.Should().Be(markdownReport);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public void OperationTimer_ShouldTrackCheckpoints()
        {
            // Arrange
            var monitor = new PerformanceMonitor();

            // Act
            using (var timer = monitor.BeginOperation("CheckpointTest", "Test"))
            {
                Thread.Sleep(50);
                timer.Checkpoint("Step1");
                Thread.Sleep(50);
                timer.Checkpoint("Step2");
                Thread.Sleep(50);

                // Assert
                var checkpoints = timer.GetCheckpoints();
                checkpoints.Should().HaveCount(2);
                checkpoints[0].Name.Should().Be("Step1");
                checkpoints[1].Name.Should().Be("Step2");
                checkpoints[1].ElapsedMs.Should().BeGreaterThan(checkpoints[0].ElapsedMs);
            }
        }
    }
}
