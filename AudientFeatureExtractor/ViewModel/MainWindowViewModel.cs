﻿using System;
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

namespace AudientFeatureExtractor.ViewModel
{
    public class MainWindowViewModel : BaseViewModel
    {
        public string genre = String.Empty;
        public MainWindowViewModel()
        {
            Genre = "GENRE";
            Extract = new AsyncCommand(extractFeatures2);
        }


        public string Genre
        {
            get { return genre; }
            set
            {
                SetProperty(ref genre, value);
            }
        }

        public string datasetName="Dataset";
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
            await Task.Run(() => extractFeatures());
            //Write non-blocking code here.
        }
        public Task extractFeatures()
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

            var folders = Directory.GetDirectories(Path.Combine(Environment.CurrentDirectory, datasetName));
            totalGenres = folders.Length;
            finishedGenres = 1;
            using (var writer = File.CreateText(Path.Combine(Environment.CurrentDirectory, "Data.csv")))
            {
                //Write header
                var main_header = "genre,";
                main_header += String.Join(",", mfccExtractor.FeatureDescriptions);
                main_header += ",";
                main_header += String.Join(",", tdExtractor.FeatureDescriptions);
                main_header += ",centroid,spread,flatness,noiseness,roloff,crest,decrease,spectral_entropy";
                writer.WriteLine(main_header);
                string feature_string = String.Empty;
                foreach (var folder in folders)
                {
                    var f_name = new DirectoryInfo(folder).Name;
                    Genre = f_name.ToUpperInvariant();
                    Prog = finishedGenres * 100 / totalGenres;
                    var files = Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, datasetName, folder));
                    //Write the genre label here
                    Console.WriteLine($"{f_name}");
                    int processedFiles = 0;
                    foreach (var filename in files)
                    {
                        MiniProg = processedFiles*100/ files.Length;
                        feature_string = String.Empty;
                        feature_string = $"{f_name},";
                        //MFCC
                        var avg_vec_mfcc = new List<float>(mfcc_no + 1);
                        //TD Features
                        var avg_vec_td = new List<float>(4);
                        //Spectral features
                        var avg_vec_spect = new List<float>(10);

                        for (var i = 0; i < mfcc_no; i++)
                        {
                            avg_vec_mfcc.Add(0f);
                        }
                        for (var i = 0; i < 4; i++)
                        {
                            avg_vec_td.Add(0f);
                        }

                        for (var i = 0; i < 10; i++)
                        {
                            avg_vec_spect.Add(0f);
                        }

                        string specFeatures = String.Empty;
                        using (var stream = new FileStream(Path.Combine(Environment.CurrentDirectory, datasetName, filename), FileMode.Open))
                        {
                            var waveFile = new WaveFile(stream);
                            signal = waveFile[Channels.Average];
                            //Compute MFCC
                            tdVectors = tdExtractor.ComputeFrom(signal);
                            mfccVectors = mfccExtractor.ComputeFrom(signal);
                            var fftSize = 1024;
                            var fft = new Fft(fftSize);
                            var resolution = (float)samplingRate / fftSize;

                            var frequencies = Enumerable.Range(0, fftSize / 2 + 1)
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
                                avg_vec_mfcc[i] += inst[i];
                            }
                        }

                        foreach (var inst in tdVectors)
                        {
                            for (var i = 0; i < 4; i++)
                            {
                                avg_vec_td[i] += inst[i];
                            }
                        }

                        for (var i = 0; i < mfcc_no; i++)
                        {
                            avg_vec_mfcc[i] /= mfccVectors.Count;
                        }

                        for (var i = 0; i < 4; i++)
                        {
                            avg_vec_td[i] /= tdVectors.Count;
                        }


                        // Write MFCCs
                        feature_string += String.Join(",", avg_vec_mfcc);
                        feature_string += ",";
                        feature_string += String.Join(",", avg_vec_td);
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
            return null;
        }

        public IAsyncCommand Extract { get; private set; }
    }
}