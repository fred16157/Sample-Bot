using Discord.Commands;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace DiscordServerManager
{
    public class Note : ModuleBase
    {
        [Command("save", RunMode = RunMode.Async)]
        public async void SaveNote(string FileName, string TextToWrite)
        {
            await ReplyAsync(FileName + ".txt에 텍스트를 기록하고 있습니다...");
            FileStream _Note = File.Open(AppDomain.CurrentDomain.BaseDirectory + "\\" + FileName + ".txt", FileMode.Create);
            foreach(char CharToWrite in TextToWrite)
            {
                foreach (byte ByteToWrite in BitConverter.GetBytes(CharToWrite))
                {
                    _Note.WriteByte(ByteToWrite);
                }
            }
            await ReplyAsync(FileName + ".txt에 텍스트가 기록되었습니다.");
        }
    }
}
