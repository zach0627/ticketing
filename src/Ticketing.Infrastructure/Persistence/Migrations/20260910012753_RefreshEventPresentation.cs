using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketing.Infrastructure.Persistence.Migrations;

/// <summary>更新活動文案與主視覺；保留活動識別、場次、座位與交易資料。</summary>
public partial class RefreshEventPresentation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
                UPDATE [Events] SET [Title] = N'LUMINA《BLOOM》台北演唱會', [Performer] = N'LUMINA', [Genre] = N'K-pop', [Description] = N'五人女子團體 LUMINA 帶著全新《BLOOM》舞台來到台北。從俐落舞曲到明亮的流行旋律，以完整編舞、全新視覺與現場樂手，展開一場屬於你我的盛放時刻。', [ImagePath] = N'/assets/events/C01-v2.webp' WHERE [Id] = 1 AND [Code] = 'C01';
                UPDATE [Events] SET [Title] = N'周以川《沿途有光》巡迴演唱會', [Performer] = N'周以川', [Genre] = N'華語流行', [Description] = N'把旅途中聽見的故事，唱成陪伴你的歌。周以川《沿途有光》巡迴以全新樂團編制，串起溫柔抒情與輕快流行，在暖色燈光下找回屬於自己的節奏。', [ImagePath] = N'/assets/events/C02-v2.webp' WHERE [Id] = 2 AND [Code] = 'C02';
                UPDATE [Events] SET [Title] = N'逆風植栽《噪音花園》LIVE', [Performer] = N'逆風植栽', [Genre] = N'獨立搖滾', [Description] = N'吉他聲浪、鼓點與不肯安靜的日常，長成逆風植栽的噪音花園。四人樂團以直率的現場能量與新作曲目，邀你一起把心裡的聲音唱大聲。', [ImagePath] = N'/assets/events/C03-v2.webp' WHERE [Id] = 3 AND [Code] = 'C03';
                UPDATE [Events] SET [Title] = N'許若棠《午夜留聲》爵士之夜', [Performer] = N'許若棠爵士四重奏', [Genre] = N'爵士', [Description] = N'讓鋼琴、薩克斯風、低音提琴與鼓聲，在夜裡交會。許若棠爵士四重奏以原創作品與即興對話，陪你走進一場從容而細膩的音樂現場。', [ImagePath] = N'/assets/events/C04-v2.webp' WHERE [Id] = 4 AND [Code] = 'C04';
                UPDATE [Events] SET [Title] = N'Signal Room《微光頻率》電子現場', [Performer] = N'Signal Room', [Genre] = N'電子音樂', [Description] = N'低頻、合成器與光線在現場交錯。Signal Room 以原創電子樂、即時編曲與節奏變化，帶來一段從微光出發、持續升溫的聲音旅程。', [ImagePath] = N'/assets/events/C05-v2.webp' WHERE [Id] = 5 AND [Code] = 'C05';
                UPDATE [Events] SET [Title] = N'江沐禾《山海之間》不插電音樂會', [Performer] = N'江沐禾', [Genre] = N'民謠', [Description] = N'一把木吉他，幾段關於山海與生活的故事。江沐禾以不插電編制呈現原創民謠，讓人聲與弦音在留白之間，慢慢靠近每一個聽眾。', [ImagePath] = N'/assets/events/C06-v2.webp' WHERE [Id] = 6 AND [Code] = 'C06';
                UPDATE [Events] SET [Title] = N'陸承恩《慢速心跳》R&B LIVE', [Performer] = N'陸承恩', [Genre] = N'R&B', [Description] = N'靈魂嗓音與律動節拍，唱出城市裡的靠近與想念。陸承恩《慢速心跳》以 R&B 為主軸，加入現場樂手與重新編曲，讓每首歌多一點呼吸。', [ImagePath] = N'/assets/events/C07-v2.webp' WHERE [Id] = 7 AND [Code] = 'C07';
                UPDATE [Events] SET [Title] = N'青杉弦樂團《弦上風景》音樂會', [Performer] = N'青杉弦樂團', [Genre] = N'跨界古典', [Description] = N'從古典弦樂走向電影感的聲音風景。青杉弦樂團以弦樂合奏與跨界編曲，帶你聽見熟悉樂器的不同表情，感受旋律層層展開的瞬間。', [ImagePath] = N'/assets/events/C08-v2.webp' WHERE [Id] = 8 AND [Code] = 'C08';
                UPDATE [Events] SET [Title] = N'程以律《街角回聲》巡演', [Performer] = N'程以律', [Genre] = N'嘻哈', [Description] = N'把街角的觀察寫成押韻，讓生活的節奏走進舞台。程以律以原創嘻哈作品、DJ 現場與俐落演出，帶來一場有態度也有故事的聲音聚會。', [ImagePath] = N'/assets/events/C09-v2.webp' WHERE [Id] = 9 AND [Code] = 'C09';
                UPDATE [Events] SET [Title] = N'ORBIT FOUR《平行星球》世界巡演', [Performer] = N'ORBIT FOUR', [Genre] = N'K-pop', [Description] = N'四人男子團體 ORBIT FOUR 以精準舞步與流行旋律，打開《平行星球》的入口。全新巡演舞台結合層次豐富的燈光、舞曲與抒情作品，讓每一個座位都能感受現場能量。', [ImagePath] = N'/assets/events/C10-v2.webp' WHERE [Id] = 10 AND [Code] = 'C10';
                UPDATE [Events] SET [Title] = N'白映寧《雨停以前》鋼琴獨奏會', [Performer] = N'白映寧', [Genre] = N'鋼琴', [Description] = N'以黑白琴鍵，記下雨聲與日光。白映寧的鋼琴獨奏會將原創旋律與細膩觸鍵放在舞台中央，邀請你為生活留下一段安靜聆聽的時間。', [ImagePath] = N'/assets/events/C11-v2.webp' WHERE [Id] = 11 AND [Code] = 'C11';
                UPDATE [Events] SET [Title] = N'潮線樂團《港灣邊界》LIVE TOUR', [Performer] = N'潮線樂團', [Genre] = N'另類搖滾', [Description] = N'潮線樂團帶著海風般開闊的吉他音色與厚實鼓點，唱出城市邊界的故事。《港灣邊界》現場巡演以全新歌單與直接的樂團演出，邀你一起迎向聲浪。', [ImagePath] = N'/assets/events/C12-v2.webp' WHERE [Id] = 12 AND [Code] = 'C12';
                UPDATE [Events] SET [Title] = N'港都海鷗 vs 山城野牛｜棒球例行賽', [Performer] = N'港都海鷗／山城野牛', [Genre] = N'棒球', [Description] = N'走進球場，感受每一次揮棒與跑壘的心跳。港都海鷗主場迎戰山城野牛，在看台上跟著節奏一起加油，享受屬於棒球的熱血午後。', [ImagePath] = N'/assets/events/S01-v2.webp' WHERE [Id] = 13 AND [Code] = 'S01';
                UPDATE [Events] SET [Title] = N'星城疾風 vs 河岸巨人｜籃球例行賽', [Performer] = N'星城疾風／河岸巨人', [Genre] = N'籃球', [Description] = N'攻防轉換就在一瞬間。星城疾風與河岸巨人將在主場交鋒，從第一聲哨音到最後一球，邀你在看台上一起感受籃球的速度與張力。', [ImagePath] = N'/assets/events/S02-v2.webp' WHERE [Id] = 14 AND [Code] = 'S02';
                UPDATE [Events] SET [Title] = N'青原 FC vs 白港 FC｜足球聯賽', [Performer] = N'青原 FC／白港 FC', [Genre] = N'足球', [Description] = N'在綠茵場邊，見證每一次推進與射門。青原 FC 主場對戰白港 FC，從開場到終場，讓整座看台的歡呼陪你度過一個熱血的比賽日。', [ImagePath] = N'/assets/events/S03-v2.webp' WHERE [Id] = 15 AND [Code] = 'S03';
                """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
                UPDATE [Events] SET [Title] = N'夜航之後', [Performer] = N'林予晴', [Genre] = N'流行', [Description] = N'冷藍舞台與近距離歌聲，從城市夜晚唱到第一道曙光。', [ImagePath] = N'/assets/events/C01.png' WHERE [Id] = 1 AND [Code] = 'C01';
                UPDATE [Events] SET [Title] = N'沿途有光', [Performer] = N'周以川', [Genre] = N'民謠流行', [Description] = N'木吉他、旅途故事與一段留給觀眾合唱的夜晚。', [ImagePath] = N'/assets/events/C02.png' WHERE [Id] = 2 AND [Code] = 'C02';
                UPDATE [Events] SET [Title] = N'噪音花園', [Performer] = N'逆風植栽', [Genre] = N'獨立搖滾', [Description] = N'三人樂團帶來不過度修飾的吉他與節奏，座位區也能感受現場能量。', [ImagePath] = N'/assets/events/C03.png' WHERE [Id] = 3 AND [Code] = 'C03';
                UPDATE [Events] SET [Title] = N'午夜留聲', [Performer] = N'許若棠爵士四重奏', [Genre] = N'爵士', [Description] = N'人聲與爵士四重奏的近距離對話，適合靜心聆聽。', [ImagePath] = N'/assets/events/C04.png' WHERE [Id] = 4 AND [Code] = 'C04';
                UPDATE [Events] SET [Title] = N'微光頻率', [Performer] = N'Signal Room', [Genre] = N'電子', [Description] = N'電子節奏與低彩度燈光構成一場有座位的沉浸演出。', [ImagePath] = N'/assets/events/C05.png' WHERE [Id] = 5 AND [Code] = 'C05';
                UPDATE [Events] SET [Title] = N'山海之間', [Performer] = N'江沐禾', [Genre] = N'民謠', [Description] = N'夕陽、木吉他與山海故事；戶外場採固定編號座席。', [ImagePath] = N'/assets/events/C06.png' WHERE [Id] = 6 AND [Code] = 'C06';
                UPDATE [Events] SET [Title] = N'慢速心跳', [Performer] = N'陸承恩', [Genre] = N'R&B', [Description] = N'溫暖人聲搭配柔軟節奏，讓忙碌的夜晚慢下來。', [ImagePath] = N'/assets/events/C07.png' WHERE [Id] = 7 AND [Code] = 'C07';
                UPDATE [Events] SET [Title] = N'弦上風景', [Performer] = N'青杉弦樂團', [Genre] = N'跨界古典', [Description] = N'四把弦樂器描繪季節與旅程，演出中請保持安靜。', [ImagePath] = N'/assets/events/C08.png' WHERE [Id] = 8 AND [Code] = 'C08';
                UPDATE [Events] SET [Title] = N'街角回聲', [Performer] = N'程以律', [Genre] = N'嘻哈', [Description] = N'以城市日常為靈感的節拍與說唱，整場採指定座位。', [ImagePath] = N'/assets/events/C09.png' WHERE [Id] = 9 AND [Code] = 'C09';
                UPDATE [Events] SET [Title] = N'平行星球', [Performer] = N'Orbit Four', [Genre] = N'團體流行', [Description] = N'四人團體帶來舞蹈、和聲與一場明亮的週末演出。', [ImagePath] = N'/assets/events/C10.png' WHERE [Id] = 10 AND [Code] = 'C10';
                UPDATE [Events] SET [Title] = N'雨停以前', [Performer] = N'白映寧', [Genre] = N'鋼琴', [Description] = N'鋼琴獨奏與短篇故事，從一個音符開始靠近彼此。', [ImagePath] = N'/assets/events/C11.png' WHERE [Id] = 11 AND [Code] = 'C11';
                UPDATE [Events] SET [Title] = N'港灣邊界', [Performer] = N'潮線樂團', [Genre] = N'另類搖滾', [Description] = N'海風中的晚場搖滾；本場提供候位室，方便展示公平入場流程。', [ImagePath] = N'/assets/events/C12.png' WHERE [Id] = 12 AND [Code] = 'C12';
                UPDATE [Events] SET [Title] = N'港都海鷗 vs 山城野牛', [Performer] = N'港都海鷗／山城野牛', [Genre] = N'棒球', [Description] = N'虛構友誼賽；可選內野、沿線或外野指定席，支援家庭連號配位。', [ImagePath] = N'/assets/events/S01.png' WHERE [Id] = 13 AND [Code] = 'S01';
                UPDATE [Events] SET [Title] = N'星城疾風 vs 河岸巨人', [Performer] = N'星城疾風／河岸巨人', [Genre] = N'籃球', [Description] = N'室內籃球對戰；依近場、側翼與上層看台分區，全區編號入座。', [ImagePath] = N'/assets/events/S02.png' WHERE [Id] = 14 AND [Code] = 'S02';
                UPDATE [Events] SET [Title] = N'青原FC vs 白港FC', [Performer] = N'青原FC／白港FC', [Genre] = N'足球', [Description] = N'傍晚足球友誼賽；主看台、側看台與球門後方皆為固定座位。', [ImagePath] = N'/assets/events/S03.png' WHERE [Id] = 15 AND [Code] = 'S03';
                """);
    }
}
