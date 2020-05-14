using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices.MVVM;
using MvvmHelpers;
using NWaves.Audio;
using NWaves.FeatureExtractors;
using NWaves.FeatureExtractors.Multi;
using NWaves.FeatureExtractors.Options;
using NWaves.Features;
using NWaves.Signals;
using NWaves.Transforms;
using NWaves.Windows;
using System.Windows.Input;
using System.Diagnostics;
using System.Drawing;
using MathNet.Numerics.Statistics;

namespace AudientFeatureExtractor.ViewModel
{
    public class MainWindowViewModel : BaseViewModel
    {
        public string genre = String.Empty;
        public MainWindowViewModel()
        {
            Genre = "GENRE";
            OutputFolder = "Spectrograms";
            DatasetName = "Dataset";
            SpecBool = true;
            Extract = new AsyncCommand(extractFeatures2);
        }

        public void startRecording()
        {

        }

        public bool specBool;
        public bool SpecBool
        {
            get { return specBool; }
            set { SetProperty(ref specBool, value); }
        }
        public string Genre
        {
            get { return genre; }
            set
            {
                SetProperty(ref genre, value);
            }
        }
        public string outputFolder = String.Empty;
        public string OutputFolder
        {
            set { SetProperty(ref outputFolder, value); }
            get { return outputFolder; }
        }

        public string datasetName = String.Empty;
        public string DatasetName
        {
            set { SetProperty(ref datasetName, value); }
            get { return datasetName; }
        }

        public int finishedGenres = 0;
        public int totalGenres = 0;
        public int prog=0;
        public int Prog
        {
            get { return prog; }
            set { SetProperty(ref prog, value); }
        }


        public int miniprog = 0;
        public int MiniProg
        {
            get { return miniprog; }
            set { SetProperty(ref miniprog, value); }
        }

        private static IList<float[]> mfccVectors;
        private static IList<float[]> tdVectors;
        public async Task extractFeatures2()
        {
            IsBusy = true;
            Genre = "Let's go!";
            Prog = 1;
            if (specBool)
            {
                await Task.Run(() => extractFeatures_spec());
            }
            else
            {
                await Task.Run(() => extractFeatures());
            }
            //Write non-blocking code here.
        }
        public async Task<int> extractFeatures()
        {
            IsBusy = true;
            DiscreteSignal signal;

            // load
            var mfcc_no = 24;
            var samplingRate = 16000;
            var mfccOptions = new MfccOptions
            {
                SamplingRate = samplingRate,
                FeatureCount = mfcc_no,
                FrameDuration = 0.025/*sec*/,
                HopDuration = 0.010/*sec*/,
                PreEmphasis = 0.97,
                Window = WindowTypes.Hamming
            };

            var opts = new MultiFeatureOptions
            {
                SamplingRate = samplingRate,
                FrameDuration = 0.025,
                HopDuration = 0.010
            };
            var tdExtractor = new TimeDomainFeaturesExtractor(opts);
            var mfccExtractor = new MfccExtractor(mfccOptions);

            var folders = Directory.GetDirectories(Path.Combine(Environment.CurrentDirectory, DatasetName));
            totalGenres = folders.Length;
            finishedGenres = 1;
            using (var writer = File.CreateText(Path.Combine(Environment.CurrentDirectory, "Data.csv")))
            {
                //Write header
                var main_header = "genre,";
                var preheader = String.Empty;
                foreach(string m in mfccExtractor.FeatureDescriptions)
                {
                    preheader += m + "_mean,";
                    preheader += m + "_min,";
                    preheader += m + "_var,";
                    preheader += m + "_sd,";
                    preheader += m + "_med,";
                    preheader += m + "_lq,";
                    preheader += m + "_uq,";
                    preheader += m + "_skew,";
                    preheader += m + "_kurt,";
                }
                foreach (string m in tdExtractor.FeatureDescriptions)
                {
                    preheader += m + "_mean,";
                    preheader += m + "_min,";
                    preheader += m + "_var,";
                    preheader += m + "_sd,";
                    preheader += m + "_med,";
                    preheader += m + "_lq,";
                    preheader += m + "_uq,";
                    preheader += m + "_skew,";
                    preheader += m + "_kurt,";
                }
                main_header += String.Join(",", preheader);
                main_header += ",centroid,spread,flatness,noiseness,roloff,crest,decrease,spectral_entropy";
                writer.WriteLine(main_header);
                Debug.WriteLine(main_header);
                string feature_string = String.Empty;
                foreach (var folder in folders)
                {
                    var f_name = new DirectoryInfo(folder).Name;
                    Genre = f_name.ToUpperInvariant();
                    Prog = finishedGenres * 100 / totalGenres;
                    var files = Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, DatasetName, folder));
                    //Write the genre label here
                    Console.WriteLine($"{f_name}");
                    int processedFiles = 0;
                    foreach (var filename in files)
                    {
                        var tmp = new FileInfo(filename).Name;
                        Genre = f_name.ToUpperInvariant() + $" {tmp}";
                        MiniProg = processedFiles*100/ files.Length;
                        feature_string = String.Empty;
                        feature_string = $"{f_name},";

                        var mfccList = new List<List<double>>();
                        var tdList = new List<List<double>>();
                        //MFCC
                        //TD Features
                        //Spectral features
                        for (var i = 0; i < mfcc_no; i++)
                        {
                            mfccList.Add(new List<double>());
                        }
                        for (var i = 0; i < 4; i++)
                        {
                            tdList.Add(new List<double>());
                        }

                        Debug.WriteLine($"{filename}");
                        string specFeatures = String.Empty;
                        using (var stream = new FileStream(Path.Combine(Environment.CurrentDirectory, DatasetName, filename), FileMode.Open))
                        {
                            var waveFile = new WaveFile(stream);
                            signal = waveFile[Channels.Average];
                            //Compute MFCC
                            tdVectors = tdExtractor.ComputeFrom(signal);
                            mfccVectors = mfccExtractor.ComputeFrom(signal);
                            var fftSize = 2048;
                            var fft = new Fft(fftSize);
                            var resolution = (float)samplingRate / fftSize;

                            //var frequencies = new float[] { 300, 500, 800, 1200, 1600, 1800, 2500, 5000/*Hz*/ };

                            var frequencies = Enumerable.Range(300, fftSize / 2 + 1)
                                                        .Select(f => f * resolution)
                                                        .ToArray();

                            var spectrum = new Fft(fftSize).MagnitudeSpectrum(signal).Samples;

                            var centroid = Spectral.Centroid(spectrum, frequencies);
                            var spread = Spectral.Spread(spectrum, frequencies);
                            var flatness = Spectral.Flatness(spectrum, 0);
                            var noiseness = Spectral.Noiseness(spectrum, frequencies, 3000);
                            var rolloff = Spectral.Rolloff(spectrum, frequencies, 0.85f);
                            var crest = Spectral.Crest(spectrum);
                            var decrease = Spectral.Decrease(spectrum);
                            var entropy = Spectral.Entropy(spectrum);
                            specFeatures = $"{centroid},{spread},{flatness},{noiseness},{rolloff},{crest},{decrease},{entropy}";
                        }

                        

                        foreach (var inst in mfccVectors)
                        {
                            for (var i = 0; i < mfcc_no; i++)
                            {
                                mfccList[i].Add(inst[i]);
                            }
                        }

                        Statistics.Mean(mfccList[1]);
                        foreach (var inst in tdVectors)
                        {
                            for (var i = 0; i < 4; i++)
                            {
                                tdList[i].Add(inst[i]);
                            }
                        }

                        var mfcc_statistics = new List<double>();
                        for (var i = 0; i < mfcc_no; i++)
                        {
                            //preheader += m + "_mean";
                            //preheader += m + "_min";
                            //preheader += m + "_var";
                            //preheader += m + "_sd";
                            //preheader += m + "_med";
                            //preheader += m + "_lq";
                            //preheader += m + "_uq";
                            //preheader += m + "_skew";
                            //preheader += m + "_kurt";
                            mfcc_statistics.Add(Statistics.Mean(mfccList[i]));
                            mfcc_statistics.Add(Statistics.Minimum(mfccList[i]));
                            mfcc_statistics.Add(Statistics.Variance(mfccList[i]));
                            mfcc_statistics.Add(Statistics.StandardDeviation(mfccList[i]));
                            mfcc_statistics.Add(Statistics.Median(mfccList[i]));
                            mfcc_statistics.Add(Statistics.LowerQuartile(mfccList[i]));
                            mfcc_statistics.Add(Statistics.UpperQuartile(mfccList[i]));
                            mfcc_statistics.Add(Statistics.Skewness(mfccList[i]));
                            mfcc_statistics.Add(Statistics.Kurtosis(mfccList[i]));
                        }
                        var td_statistics = new List<double>();

                        for( var i=0; i<4; i++)
                        {
                            td_statistics.Add(Statistics.Mean(tdList[i]));
                            td_statistics.Add(Statistics.Minimum(tdList[i]));
                            td_statistics.Add(Statistics.Variance(tdList[i]));
                            td_statistics.Add(Statistics.StandardDeviation(tdList[i]));
                            td_statistics.Add(Statistics.Median(tdList[i]));
                            td_statistics.Add(Statistics.LowerQuartile(tdList[i]));
                            td_statistics.Add(Statistics.UpperQuartile(tdList[i]));
                            td_statistics.Add(Statistics.Skewness(tdList[i]));
                            td_statistics.Add(Statistics.Kurtosis(tdList[i]));
                        }

                        // Write MFCCs
                        feature_string += String.Join(",", mfcc_statistics);
                        feature_string += ",";
                        feature_string += String.Join(",", td_statistics);
                        //Write Spectral features as well
                        feature_string += ",";
                        feature_string += specFeatures;
                        writer.WriteLine(feature_string);
                        var file_name = new DirectoryInfo(filename).Name;
                        Console.WriteLine($"{file_name}");
                        processedFiles += 1;
                    }
                    finishedGenres += 1;
                }
            }
            return 0;
        }

        public IAsyncCommand Extract { get; private set; }

        public IAsyncCommand recordWave { get; }

        public async Task<int> extractFeatures_spec()
        {
            Directory.CreateDirectory(OutputFolder);
            Debug.WriteLine("Extracting spectrograms.");
            var folders = Directory.GetDirectories(Path.Combine(Environment.CurrentDirectory, DatasetName));
            totalGenres = folders.Length;
            finishedGenres = 1;
            foreach (var folder in folders)
            {
                var f_name = new DirectoryInfo(folder).Name;
                Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory,OutputFolder,f_name));
                Genre = f_name.ToUpperInvariant();
                Prog = finishedGenres * 100 / totalGenres;
                var files = Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, DatasetName, folder));
                int processedFiles = 0;
                foreach (var file in files)
                {
                    MiniProg = processedFiles * 100 / files.Length;
                    var tmp = new FileInfo(file).Name;
                    Genre = f_name.ToUpperInvariant() + $" {tmp}";
                    var file_name = tmp.Substring(0, tmp.Length - 3) + "jpg";
                    Debug.WriteLine(Path.Combine(Environment.CurrentDirectory, DatasetName, folder, file));
                    var spec = new Spectrogram.Spectrogram(sampleRate: 22000, fftSize: 2048, step: 700);
                    float[] values = Spectrogram.Tools.ReadWav(Path.Combine(Environment.CurrentDirectory, DatasetName, folder, file));
                    spec.AddExtend(values);
                    // convert FFT to an image and save it
                    Bitmap bmp = spec.GetBitmap(intensity: 2, freqHigh: 8000);
                    spec.SaveBitmap(bmp, Path.Combine(Environment.CurrentDirectory, OutputFolder, f_name, file_name));
                    processedFiles += 1;
                    Debug.WriteLine($"{OutputFolder} OP: {Path.Combine(Environment.CurrentDirectory, OutputFolder, f_name, file_name)}");
                }
                finishedGenres += 1;
            }
            return 0;
        }
    }
}
