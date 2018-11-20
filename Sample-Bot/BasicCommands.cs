using System;
using System.Timers;
using System.Threading.Tasks;
using System.Xml;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Collections.Generic;

namespace DiscordServerManager
{
    public class BasicCommands : ModuleBase
    {
        /*
         * 현재 완벽하게 작동하는 명령어 목록:
         * !userinfo, !ban, !kick, !logs, !errorinfo
         * 현재 작동하지 않는 명령어 목록:
         * !voteban, !votekick, !yes, !no
         */
        Timer VoteTimer = null;
        bool VoteRunning;
        List<IUser> UpVotersList = new List<IUser>();
        
        SocketGuildUser TargetUser;
        bool IsBan;
        string VoteReason;

        [Command("userinfo")]
        [Alias("user", "whois")]
        public async Task ReturnInfo(IUser TargetUser = null)
        {
            var UserInfo = TargetUser ?? Context.Client.CurrentUser;
            EmbedBuilder Embed = new EmbedBuilder();
            Embed.AddField("**이름**", UserInfo.Username);
            Embed.AddInlineField("**태그**", UserInfo.Discriminator);
            Embed.AddInlineField("**계정 생성일**", UserInfo.CreatedAt);
            Embed.AddField("**계정 식별자**", UserInfo.Id);
            Embed.Color = new Color(0, 255, 0);
            await ReplyAsync("",false,Embed.Build());
        }

        [Command("random"),Alias("랜덤")]
        public async Task Rand(params string[] Parameters)
        {
            Random rand = new Random(DateTime.Now.Millisecond);
            await ReplyAsync("결과: " + Parameters[rand.Next(Parameters.Length)]);
        }

        [RequireUserPermission(GuildPermission.BanMembers)]
        [Command("ban"), Alias("밴")]
        public async Task DoBan(IUser TargetUser = null, string BanReason = "")
        {
            if(TargetUser == null)
            {
                await ReplyAsync("ban 사용법: !ban 또는 !밴 <밴 대상> <사유(선택)>");
                return;
            }
            await Context.Guild.AddBanAsync(TargetUser, 0, BanReason);
            EmbedBuilder Embed = new EmbedBuilder();
            if(BanReason == string.Empty)
            {
                Embed.Description = "**"+Context.User.Username + "님이 " + TargetUser.Username + "에게 밴을 때렸습니다.**";
            }
            else
            {
                Embed.Description = "**"+Context.User.Username + "님이 " + TargetUser.Username + "에게 " + BanReason + "의 이유로 밴을 떄렸습니다.**";
            }
            Embed.Color = new Color(0, 255, 0);
            await ReplyAsync("", false, Embed.Build());
        }
        [RequireUserPermission(GuildPermission.KickMembers)]
        [Command("kick"), Alias("킥")]
        public async Task DoKick(SocketGuildUser TargetUser = null, string KickReason = "")
        {
            if (TargetUser == null)
            {
                await ReplyAsync("kick 사용법: !kick 또는 !킥 <추방 대상> <사유(선택)>");
                return;
            }
            await TargetUser.KickAsync(KickReason);
            EmbedBuilder Embed = new EmbedBuilder();
            if (KickReason == string.Empty)
            {
                Embed.Description = "**"+Context.User.Username + "님이 " + TargetUser.Username + "를 추방했습니다.**";
            }
            else
            {
                Embed.Description = "**"+Context.User.Username + "님이 " + TargetUser.Username + "를 " + KickReason + "의 이유로 추방했습니다.**";
            }
            Embed.Color = new Color(0, 255, 0);
            await ReplyAsync("", false, Embed.Build());
        }

        [Command("logs"), Alias("로그")]
        public async Task ReturnLog()
        {
            await Context.User.SendMessageAsync("현재 세션의 로그입니다: \n" + MainWindow.LogText);
        }

        [Command("errorinfo"),Alias("에러정보")]
        public async Task ReturnErrorInfo(int ErrorNo = 0)
        {
            if(ErrorNo == 0)
            {
                await ReplyAsync("errorinfo 사용법: !errorinfo 또는 !에러정보 <에러번호>");
            }
            if (!MainWindow.ErrorInfos.TryGetValue(ErrorNo, out ErrorInfo CommandErrorInfo))
            {
                await ReplyAsync("유효하지 않은 에러번호입니다.");
            }
            EmbedBuilder Embed = new EmbedBuilder
            {
                Title = "**에러번호 #" + ErrorNo + " 에 대한 정보입니다: **"
            };
            Embed.AddField("**에러 사유**", CommandErrorInfo.ErrorReason);
            Embed.AddInlineField("**발생 시각**", CommandErrorInfo.OccurTime);
            Embed.AddInlineField("**명령어 사용자**", CommandErrorInfo.OccurUser);
            Embed.AddField("**발생 서버**", CommandErrorInfo.OccurGuild.Name);
            Embed.AddField("**발생 채널**", CommandErrorInfo.OccurChannel.Name);
            await Context.User.SendMessageAsync("",false,Embed.Build());
        }
        
        [Command("votekick"),Alias("추방투표")]
        public async Task VoteKick(SocketGuildUser TargetUser = null,double Seconds = 60,string VoteReason = null)
        {
            if (TargetUser == null)
            {
                await ReplyAsync("votekick 사용법: !votekick 또는 !추방투표 <추방 대상> <투표 시간(기본값 60초)> <이유>");
                return;
            }
            if(VoteRunning)
            {
                await ReplyAsync("현재 다른 투표가 진행중입니다. 투표가 끝날때까지 기다려 주세요.");
                return;
            }
            await MainWindow.DiscordClient.SetGameAsync("추방 투표 진행중: 찬성하려면 !yes 또는 !찬성 명령어를 사용하세요");
            VoteRunning = true;
            this.TargetUser = TargetUser;
            IsBan = false;
            this.VoteReason = VoteReason;
            VoteTimer = new Timer();
            VoteTimer.Elapsed += ReturnVoteResult;
            VoteTimer.Interval = Seconds * 1000;
            VoteTimer.Start();
            EmbedBuilder Embed = new EmbedBuilder
            {
                Description = "**추방 투표가 " + Seconds + "초 동안 진행됩니다. " + 5 + "명 이상 찬성해야 추방이 진행됩니다.\n찬성하려면 !yes 또는 !찬성 명령어를 사용하세요.**"
            };
            await ReplyAsync("", false, Embed.Build());
        }

        [Command("voteban"),Alias("밴투표")]
        public async Task VoteBan(SocketGuildUser TargetUser= null,double Seconds = 60, string VoteReason = null)
        {
            if (TargetUser == null)
            {
                await ReplyAsync("voteban 사용법: !voteban 또는 !밴투표 <밴 대상> <투표 시간(기본값 60초)> <이유>");
                return;
            }
            if (VoteRunning)
            {
                await ReplyAsync("현재 다른 투표가 진행중입니다. 투표가 끝날때까지 기다려 주세요.");
                return;
            }
            await MainWindow.DiscordClient.SetGameAsync("밴 투표 진행중: 찬성하려면 !yes 또는 !찬성 명령어를 사용하세요");
            VoteRunning = true;
            this.TargetUser = TargetUser;
            IsBan = true;
            this.VoteReason = VoteReason;
            VoteTimer = new Timer();
            VoteTimer.Elapsed += ReturnVoteResult;
            VoteTimer.Interval = Seconds * 1000;
            VoteTimer.Start();
            EmbedBuilder Embed = new EmbedBuilder
            {
                Description = "**밴 투표가 " + Seconds + "초 동안 진행됩니다. " + 5 + "명 이상 찬성해야 밴이 진행됩니다.\n찬성하려면 !yes 또는 !찬성 명령어를 사용하세요.**"
            };
            await ReplyAsync("", false, Embed.Build());
        }

        private void ReturnVoteResult(object sender, ElapsedEventArgs e)
        {
            ReturnResult();
        }

        private async Task ReturnResult()
        {
            VoteTimer.Stop();
            VoteTimer = null;
            EmbedBuilder Embed = new EmbedBuilder
            {
                Title = "투표 결과"
            };
            if (VoteReason == null)
            {
                VoteReason = "사유 없음";
            }
            if (IsBan)
            {
                Embed.AddInlineField("작업", "밴");
                Embed.AddField("밴 대상", TargetUser.Username);
                Embed.AddField("밴 사유", VoteReason);
                if (UpVotersList.Count >= 5)
                {
                    Embed.AddInlineField("결과", "밴 진행");
                }
                else if (UpVotersList.Count < 5)
                {
                    Embed.AddInlineField("결과", "투표 부결됨");
                }
                Embed.AddInlineField("찬성 투표 수", UpVotersList.Count);
                Embed.AddInlineField("요구 찬성 투표 수", 5);
                await ReplyAsync("", false, Embed.Build());
            }
            else if (!IsBan)
            {
                Embed.AddInlineField("작업", "추방");
                Embed.AddField("추방 대상", TargetUser.Username);
                Embed.AddField("추방 사유", VoteReason);
                if (UpVotersList.Count >= 5)
                {
                    Embed.AddInlineField("결과", "추방 진행");
                    await TargetUser.KickAsync();
                }
                else if (UpVotersList.Count < 5)
                {
                    Embed.AddInlineField("결과", "투표 부결됨");
                }
                Embed.AddInlineField("찬성 투표 수", UpVotersList.Count);
                Embed.AddInlineField("요구 찬성 투표 수", 5);
            }
            VoteRunning = false;
            TargetUser = null;
            IsBan = false;
            VoteReason = null;
            UpVotersList.Clear();
            await MainWindow.DiscordClient.SetGameAsync("준비됨 (!help로 명령어 목록 보기)");
            return;
        }

        [Command("yes"),Alias("찬성")]
        public async Task UpVote()
        {
            if(!VoteRunning)
            {
                await ReplyAsync("현재 진행중인 투표가 없습니다.");
                return;
            }
            if(UpVotersList.Contains(Context.User))
            {
                await ReplyAsync("이미 찬성 투표가 등록되었습니다. 찬성 투표는 한번만 가능합니다.");
                return;
            }
            UpVotersList.Add(Context.User);
            await ReplyAsync("성공적으로 찬성 투표가 등록되었습니다.");
        }
    }
}
