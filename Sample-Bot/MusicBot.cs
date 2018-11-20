using Discord.Commands;
using Discord.WebSocket;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using VideoLibrary;

using System.Diagnostics;
using Discord.Audio;
using Discord;
using System.Threading;


namespace DiscordServerManager
{
    public class MusicBot : ModuleBase
    {   
        //현재 뮤직봇 기능은 아직 테스트중입니다. 음원 추출은 제대로 되지만, 음성 채널에 스트림을 제대로 넣지 못합니다.
        public static IAudioClient Client;
        public static CancellationTokenSource _disposeToken;
        public static int SampleRate;
        public static float Volume = 100f;
        string YoutubeLink;
        string VideoName;
        [Command("summon",RunMode = RunMode.Async), Alias("소환")]
        public async Task JoinChannel(IVoiceChannel TargetChannel = null)
        {
            TargetChannel = TargetChannel ?? (Context.User as IGuildUser)?.VoiceChannel;
            if (TargetChannel == null)
            {
                await ReplyAsync("summon 사용법: !summon 또는 !소환 <채널 이름>");
                return;
            }
            Client = await TargetChannel.ConnectAsync();
            await ReplyAsync(TargetChannel.Name + "에 뮤직봇이 소환되었습니다");
        }

        public async Task SendStream()
        {
            var FFMpeg = MakeStream(VideoName).StandardOutput.BaseStream;
            var Stream = Client.CreatePCMStream(AudioApplication.Music);
            await FFMpeg.CopyToAsync(Stream);
            await Stream.FlushAsync().ConfigureAwait(false);
        }

        public async Task Play(string VideoName)
        {
            var output = MakeStream(VideoName).StandardOutput.BaseStream;
            var strm = Client.CreatePCMStream(AudioApplication.Music);
            await output.CopyToAsync(strm);
            await strm.FlushAsync().ConfigureAwait(false);
        }

        [Command("play", RunMode = RunMode.Async), Alias("재생")]
        public async Task PlayMusic(string YoutubeLink = "", IVoiceChannel Channel = null)
        {
            if (YoutubeLink == "")
            {
                await ReplyAsync("play 사용법: !play 또는 !재생 <유튜브 링크>");
                return;
            }
            this.YoutubeLink = YoutubeLink;
            Channel = Channel ?? (Context.User as IGuildUser)?.VoiceChannel;
            Client = await Channel.ConnectAsync();
            var YouTubeService = YouTube.Default;
            var Videos = YouTubeService.GetAllVideos(YoutubeLink);
            var Video = Videos.OrderByDescending(Info => Info.AudioBitrate).First();

            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + @"\Musics\" + Video.FullName.Replace(" ", "") + ".mp3"))
            {
                await ReplyAsync("다운로드 진행중: " + Video.Title);
                File.WriteAllBytes(AppDomain.CurrentDomain.BaseDirectory + @"\Musics\" + Video.FullName.Replace(" ", "") , Video.GetBytes());
                await ReplyAsync("다운로드 완료: " + Video.Title);
            }

            await ReplyAsync("재생 준비중: " + Video.Title);
            VideoName = Video.FullName.Replace(" ", "");
            Convert(VideoName);
            await Play(VideoName);
        }

        [Command("download",RunMode = RunMode.Async), Alias("저장")]
        public async Task DownloadMusic(string YoutubeLink = "")
        {
            if (YoutubeLink == "")
            {
                await ReplyAsync("download 사용법: !download 또는 !저장 <유튜브 링크>");
            }
            var YouTubeService = YouTube.Default;
            var Videos = YouTubeService.GetAllVideos(YoutubeLink);
            var Video = Videos.OrderByDescending(Info => Info.AudioBitrate).First();
            await ReplyAsync("다운로드 진행중: " + Video.Title);
            File.WriteAllBytes(AppDomain.CurrentDomain.BaseDirectory + @"\Musics\" + Video.FullName.Replace(" ", ""), Video.GetBytes());
            await ReplyAsync("다운로드 완료: " + Video.Title);

            Convert(Video.FullName.Replace(" ",""));
        }

        public Process Convert(string VideoName)
        {
            ProcessStartInfo ffmpegInfo = new ProcessStartInfo()
            {
                FileName = "C:\\ffmpeg.exe",
                Arguments = $"-i " + AppDomain.CurrentDomain.BaseDirectory + @"Musics\" + VideoName + " -ar 48000 -ac 2 -f mp3  " + @"Musics\" + VideoName + ".mp3",
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            return Process.Start(ffmpegInfo);
        }

        public Process MakeStream(string VideoName)
        {
            ProcessStartInfo ffmpegInfo = new ProcessStartInfo()
            {
                FileName = "C:\\ffmpeg.exe",
                Arguments = $"-loglevel panic -i " + AppDomain.CurrentDomain.BaseDirectory + @"Musics\" + VideoName + ".mp3" + " -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            return Process.Start(ffmpegInfo);
        }
    }
}

