using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IDScanNet.Validation;
using IDScanNet.Validation.SDK;

namespace TestApp
{
    public class Program
    {
        private static ValidationService _validationService;

        static async Task Main(string[] args)
        {
            //Init ValidationService
            Init();
            //Validate valid document
            await Validate("Valid");
            //Validate fake by UV
            await Validate("UVFailed");
            //Validate fake RawString
            await Validate("FakeByRawString");
            Console.ReadLine();
        }


        private static void Init()
        {
            Directory.CreateDirectory("Validation Logs");
            //Simple validation settings
            var validationServiceSettings = new ValidationServiceSettings
            {
                // if set to true - the host will try to launch the first available Gemalto scanner
                IsUseLocalDevices = false,
                //Set logging directory(default is c:\Users\Public\Documents\IDScan.net\Validation.SDK\Logs\)
                LoggingDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Validation Logs"),

                //Advanced validation settings:

                //Host - pipe name for connection. If it's not stated, then the default one will be used
                //Port - ignore this for now, as it would be required sometime in the future
                //HostDirectoryPath - folder with the host files
                //HostDataDirectoryPath - folder with data-files; can be substituted with a different value in case it's part of an app and data-files are somewhere close
            };

            _validationService = new ValidationService(validationServiceSettings);

            //Fired when the document processing stage is changed
            _validationService.ProcessingStageChanged += (sender, s) =>
            {
                Console.WriteLine(s.Status);
            };
            //Fired when the error has occurred
            _validationService.ErrorReceived += (sender, s) =>
            {
                Console.WriteLine(s.ToString());
            };
            //Fired when the processing of the document captured with the device is completed
            _validationService.DeviceProcessingCompleted += Service_DeviceProcessingCompleted;

            //Asynchronously initialize validation service
            _validationService.InitializeAsync();
        }

        private static async Task Validate(String folder)
        {
            Console.WriteLine("----------------------------------------------------------------------------------------");
            Console.WriteLine($"Validate {folder}:");

            //Create a new validation request
            var request = new ValidationRequest();
            request.Id = Guid.NewGuid();
            request.Scan = new ScanResult();
            //Set RawString with a RawDataSource type
            request.Scan.RawItems = new Dictionary<RawDataSource, RawData>
            {
                {
                    RawDataSource.PDF417, new RawData
                    {
                        RawString = await File.ReadAllTextAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder, "Pdf417RawData.txt"))
                    }
                }
            };
            //Set Images by ImageType
            request.Scan.ScannedImages = new Dictionary<ImageType, byte[]>
            {
                {ImageType.ColorFront, await File.ReadAllBytesAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder, "Normal.jpg"))},
                {ImageType.ColorBack, await File.ReadAllBytesAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder, "NormalBack.jpg"))},
                {ImageType.UVFront, await File.ReadAllBytesAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder, "UV.jpg"))},
                {ImageType.UVBack, await File.ReadAllBytesAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder, "UVBack.jpg"))},
                {ImageType.IRFront, await File.ReadAllBytesAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder, "IR.jpg"))},
                {ImageType.IRBack, await File.ReadAllBytesAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder, "IRBack.jpg"))},
            };
            //Asynchronously validate document
            ValidationResponse result = await _validationService.ProcessAsync(request);

            Console.WriteLine();
            Console.WriteLine($"Validation Result for {folder}:");
            if (result.Result != null)
            {
                // AuthenticationTests include UV, IR, Void validations
                foreach (var testResult in result.Result.AuthenticationTests)
                {
                    Console.WriteLine(
                        $"{testResult.Name} - {testResult.Type} {testResult.Status} {testResult.Confidence}");
                }
                //CrossMatch RawString and OCR
                foreach (var testResult in result.Result.CrossMatchTests)
                {
                    Console.WriteLine(
                        $"{testResult.Name} - {testResult.Type} {testResult.Status} {testResult.Confidence}");
                    foreach (var match in testResult.CrossMatches)
                    {
                        Console.WriteLine(
                            $"      {match.FieldName} - {match.Item1.DataSource} = {match.Item1.Value};  {match.Item2.DataSource} = {match.Item2.Value} Confidence = {testResult.Confidence}");
                    }
                }

                //RawString test
                foreach (var testResult in result.Result.DataValidationTests)
                {
                    Console.WriteLine(
                        $"{testResult.Name} - {testResult.Type} {testResult.Status} ");
                }
            }
            //Final validation result
            Console.WriteLine("Validation status = " + result.Result?.Status);
            Console.WriteLine();
        }

        private static void Service_DeviceProcessingCompleted(object sender, ValidationResponse e)
        {
            var d = e.Document;
        }
    }
}