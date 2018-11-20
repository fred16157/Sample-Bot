using System;

using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Discord;
using Discord.Commands;
using Discord.Audio;
using Discord.WebSocket;
using System.Reflection;
using System.Collections.Generic;

namespace DiscordServerManager
{
    public class ErrorInfo
    {
        public string ErrorReason { get; set; }
        public DateTime OccurTime { get; set; }
        public IGuild OccurGuild { get; set; }
        public IMessageChannel OccurChannel { get; set; }
        public IUser OccurUser { get; set; }
    }

    public class CommandLog
    {
        public string CommandMsg { get; set; }
        public DateTime CommandTime { get; set; }
        public bool IsSuccess { get; set; }
        public int ErrorNo { get; set; }
    }

    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public static DiscordSocketClient DiscordClient;
        CommandService ComServ;
        IServiceProvider ServProv;
        List<SocketGuild> ServerLists = new List<SocketGuild>();
        public static Dictionary<int, ErrorInfo> ErrorInfos = new Dictionary<int, ErrorInfo>();
        public static Dictionary<int, CommandLog> CommandLogs = new Dictionary<int, CommandLog>();
        public static string LogText;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void ExecuteBot_Btn_ClickAsync(object sender, RoutedEventArgs e)
        {
            await StartBot();
        }

        public async Task StartBot()
        {
            try
            {
                DiscordClient = new DiscordSocketClient(new DiscordSocketConfig
                {
                    LogLevel = LogSeverity.Debug
                });
                Log("디스코드 클라이언트가 초기화됨");
                ComServ = new CommandService(new CommandServiceConfig
                {
                    LogLevel = LogSeverity.Debug
                });
                Log("명령어 서비스가 초기화됨");
                DiscordClient.Log += DiscordClient_Log;
                ComServ.Log += DiscordClient_Log;

                DiscordClient.GuildAvailable += DiscordClient_GuildAvailable;
                DiscordClient.Ready += DiscordClient_Ready;
                DiscordClient.LoggedIn += DiscordClient_LoggedIn;
                DiscordClient.LatencyUpdated += DiscordClient_LatencyUpdated;
                DiscordClient.UserIsTyping += DiscordClient_UserIsTyping;
                DiscordClient.UserJoined += DiscordClient_UserJoined;
                
                await DiscordClient.LoginAsync(TokenType.Bot, TokenBox.Text, true);
                await DiscordClient.StartAsync();
                await InstallCommands();
            }
            catch (Exception Ex)
            {
                MessageBox.Show("봇 실행중 예상치 못한 에러가 발생했습니다. 스택 추적 : \n" + Ex.StackTrace, "이런!", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);
            }
        }

        private async Task DiscordClient_UserJoined(SocketGuildUser arg)
        {
            Log("다음 유저가 " + arg.Guild.Name + "에 참여했습니다: " + arg.Username);
            await arg.Guild.DefaultChannel.SendMessageAsync("다음 유저에게 기본 등급인 **미군**을 부여합니다: **" + arg.Username + "**");
            if(arg.Guild.Id == 372713490365939714)
            {
                await arg.AddRoleAsync(arg.Guild.GetRole(441151069142384650));
            }
        }

        private Task DiscordClient_UserIsTyping(SocketUser arg1, ISocketMessageChannel arg2)
        {
            Log("다음 유저가 채널 " + arg2.Name + "에서 메시지를 입력중: " + arg1.Username);
            return Task.CompletedTask;
        }

        private Task DiscordClient_LatencyUpdated(int arg1, int arg2)
        {
            LatencyBlock.Text = "네트워크 지연: " + DiscordClient.Latency + "ms";
            return Task.CompletedTask;
        }

        private Task DiscordClient_LoggedIn()
        {
            Log("디스코드 게이트웨이에 로그인됨");
            return Task.CompletedTask;
        }

        private Task DiscordClient_Ready()
        {
            Log("봇 준비 완료");
            if(ServerLists.Count > 0)
            {
                Log("다음의 서버들에 접속 가능합니다:");
                for(int i = 0; i < ServerLists.Count ; i++)
                {
                    Log("\t - " + ServerLists[i].Name);
                }
            }
            DiscordClient.SetGameAsync("준비됨 (!help로 명령어 목록 보기)");
            return Task.CompletedTask;
        }

        private Task DiscordClient_GuildAvailable(SocketGuild arg)
        {
            ServerLists.Add(arg);
            return Task.CompletedTask;
        }

        public async Task InstallCommands()
        {
            DiscordClient.MessageReceived += HandleCommand;
            Log("명령어 처리 함수가 등록됨");
            await ComServ.AddModulesAsync(Assembly.GetEntryAssembly());
            Log("명령어 모듈이 추가됨");
        }

        public async Task HandleCommand(SocketMessage MessageParam)
        {
            try
            {
                if (!(MessageParam is SocketUserMessage Message)) return;
                int ArgPos = 0;
                if (!(Message.HasCharPrefix('!', ref ArgPos) || Message.HasMentionPrefix(DiscordClient.CurrentUser, ref ArgPos)))
                {
                    return;
                }
                var Context = new CommandContext(DiscordClient, Message);
                Log(Message.Author.Username + "님의 명령어 수신됨: " + MessageParam);
                var Result = await ComServ.ExecuteAsync(Context, ArgPos, ServProv);
                if (!Result.IsSuccess)
                {
                    ErrorInfo CommandErrorInfo = new ErrorInfo
                    {
                        ErrorReason = "명령어 " + MessageParam + " (을)를 처리하는중 에러 발생: " + Result.ErrorReason,
                        OccurTime = DateTime.Now,
                        OccurUser = Context.User,
                        OccurChannel = Context.Channel,
                        OccurGuild = Context.Guild
                    };
                    ErrorInfos.Add(ErrorInfos.Count + 1, CommandErrorInfo);
                    await Context.Channel.SendMessageAsync("에러가 발생했습니다.\n!errorinfo <에러번호> 또는 !에러정보 <에러번호>로 확인 가능합니다");
                    await Context.Channel.SendMessageAsync("에러번호는 " + ErrorInfos.Count + "입니다.");
                    Log(Message.Author.Username + "님의 명령어 처리중 에러 발생: " + Result.ErrorReason);
                    return;
                }
                Log(Message.Author.Username + "님의 명령어 처리 성공: " + MessageParam);
            }
            catch(Exception Ex)
            {

            }
        }
        
        private Task DiscordClient_Log(LogMessage arg)
        {
             Dispatcher.Invoke(new Action(delegate ()
             {
                 OutputBox.AppendText("[ Discord.Net "+ DateTime.Now + " ]"  + arg.Message + "\n");
             }));
            return Task.CompletedTask;
        }

        public void Log(string Msg)
        {
            Dispatcher.Invoke(new Action(delegate ()
            {
                OutputBox.AppendText("[ " + DateTime.Now + " ]" + Msg + "\n");
            }));
        }

        private void OutputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            OutputBox.ScrollToEnd();
            LogText = OutputBox.Text;
        }
    }
}
