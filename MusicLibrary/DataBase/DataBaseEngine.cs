﻿using Microsoft.EntityFrameworkCore;
using Reborn_Zune_MusicLibraryEFCoreModel;
using Reborn_Zune_MusicLibraryService.DataModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagLib;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace Reborn_Zune_MusicLibraryService.DataBase
{
    static class DataBaseEngine
    {
        private const string UNKNOWN_ARTIST = "Unknown Artist";
        private const string UNKNOWN_ALBUM = "Unknown Album";
        private const string UNKNOWN_YEAR = "Unknown Year";

        public static void Initialize()
        {
            try
            {
                using (var db = new MusicLibraryDbContext())
                {
                    db.Database.Migrate();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }

        }

        #region Database Operation
        public static async Task Add(StorageFile File)
        {
            try
            {
                Debug.WriteLine(File.Name + " Music meta data start retreiving");


                var thumbnail = await File.GetThumbnailAsync(ThumbnailMode.MusicView, 100, ThumbnailOptions.ReturnOnlyIfCached);
                var properties = await File.Properties.GetMusicPropertiesAsync();

                var path = File.Path;
                var bytearray = await ConvertThumbnailToBytesAsync(thumbnail);
                var artistName = properties.Artist != "" ? properties.Artist : UNKNOWN_ARTIST;
                var albumArtistName = properties.AlbumArtist != "" ? properties.AlbumArtist : UNKNOWN_ARTIST;
                var albumTitle = properties.Album != "" ? properties.Album : UNKNOWN_ALBUM;
                var duration = properties.Duration.ToString(@"mm\:ss");
                var albumYear = properties.Year != 0 ? properties.Year.ToString() : UNKNOWN_YEAR;
                var songTitle = properties.Title != "" ? properties.Title : Path.GetFileNameWithoutExtension(File.Path);

                using (var _context = new MusicLibraryDbContext())
                {
                    var thumb = new Thumbnail
                    {
                        ImageBytes = bytearray,
                        Id = Guid.NewGuid().ToString()
                    };
                    _context.Thumbnails.Add(thumb);
                    _context.SaveChanges();
                    Debug.WriteLine("Thumbnail Done");

                    Music Music = new Music
                    {
                        Path = path,
                        Title = songTitle,
                        AlbumTitle = albumTitle,
                        Artist = artistName,
                        AlbumArtist = albumArtistName,
                        Year = albumYear,
                        ThumbnailId = thumb.Id,
                        Duration = duration,

                        Id = Guid.NewGuid().ToString()
                    };
                    _context.Musics.Add(Music);
                    _context.SaveChanges();
                    Debug.WriteLine("Music Done");
                }
                Debug.WriteLine("DataBase Succeed");
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }

        public static void Delete(string path)
        {
            try
            {
                using (var _context = new MusicLibraryDbContext())
                {
                    var music = _context.Musics.Where(m => m.Path == path).First();
                    _context.Musics.Remove(music);
                    _context.SaveChanges();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }

        public static async Task Update(StorageFile File)
        {
            try
            {
                Debug.WriteLine(File.Name + " Music meta data start retreiving");
                var properties = await File.Properties.GetMusicPropertiesAsync();
                var fileStream = await File.OpenStreamForReadAsync();

                var tagFile = TagLib.File.Create(new StreamFileAbstraction(File.Name,
                                 fileStream, fileStream));

                var types = tagFile.TagTypes;

                if ((types & (TagTypes.Id3v1 | TagTypes.Id3v2)) == (TagTypes.Id3v1 | TagTypes.Id3v2))
                {
                    types = TagTypes.Id3v2;
                }

                var tags = tagFile.GetTag(types);

                var path = File.Path;
                var bytearray = tags.Pictures.Length == 0 ? new byte[] { } : tags.Pictures[0].Data.Data;
                var artistName = properties.Artist != "" ? properties.AlbumArtist : UNKNOWN_ARTIST;
                var albumArtistName = properties.AlbumArtist != null ? properties.AlbumArtist : UNKNOWN_ARTIST;
                var albumTitle = properties.Album != "" ? properties.Album : UNKNOWN_ALBUM;
                var duration = properties.Duration.ToString(@"mm\:ss");
                var albumYear = properties.Year != 0 ? properties.Year.ToString() : UNKNOWN_YEAR;
                var songTitle = properties.Title != "" ? properties.Title : Path.GetFileNameWithoutExtension(File.Path);

                Debug.WriteLine("Access into database");
                using (var _context = new MusicLibraryDbContext())
                {
                    Music music = _context.Musics.Where(m => m.Path == path).First();
                    Thumbnail thumbnail = _context.Thumbnails.Where(t => t.Id == music.ThumbnailId).First();

                    music.Artist = artistName;
                    music.AlbumArtist = albumArtistName;
                    music.AlbumTitle = albumTitle;
                    music.Title = songTitle;
                    music.Year = albumYear;
                    music.Duration = duration;

                    _context.Musics.Update(music);

                    if (thumbnail.ImageBytes != bytearray)
                    {
                        thumbnail.ImageBytes = bytearray;
                        _context.Thumbnails.Update(thumbnail);
                    }


                    _context.SaveChanges();
                }
                Debug.WriteLine("Update Succeed");
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }

        public static void Update(KeyValuePair<string, string> pathChange)
        {
            try
            {
                Debug.WriteLine("Access into database");
                using (var _context = new MusicLibraryDbContext())
                {
                    Music music = _context.Musics.Where(m => m.Path == pathChange.Key).First();
                    music.Path = pathChange.Value;
                    _context.Musics.Update(music);
                    _context.SaveChanges();
                }
                Debug.WriteLine("Update Succeed");
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }

        public static Library FetchAll()
        {
            Library library = new Library();
            try
            {
                using (var _context = new MusicLibraryDbContext())
                {
                    library.Musics = _context.Musics.Select(m => new MLMusicModel(m)).ToList();
                    library.MInP = _context.MusicInPlaylists.Select(m => new MLMusicInPlaylistModel(m)).ToList();
                    library.Playlists = _context.Playlists.Select(p => new MLPlayListModel(p)).ToList();
                    library.Thumbnails = _context.Thumbnails.Select(t => new MLThumbnailModel(t)).ToList();
                }
                return library;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                return library;
            }
        }
        #endregion

        #region Helper
        public static bool PlaylistNameAvailable(string playlistName)
        {
            using (var _context = new MusicLibraryDbContext())
            {
                return (_context.Playlists.Where(p => p.Name == playlistName).FirstOrDefault() == null);
            }
        }
        private static async Task<byte[]> ConvertThumbnailToBytesAsync(StorageItemThumbnail thumbnail)
        {
            if (thumbnail == null)
            {
                return new byte[0];
            }
            byte[] result = new byte[thumbnail.Size];
            using (var reader = new DataReader(thumbnail))
            {
                await reader.LoadAsync((uint)thumbnail.Size);
                reader.ReadBytes(result);
                return result;
            }
        }
        #endregion

        #region Playlist Operation
        public static void CreatePlaylist(string playlistName)
        {
            Playlist playlist = new Playlist
            {
                Name = playlistName,
                Id = Guid.NewGuid().ToString()
            };
            using (var _context = new MusicLibraryDbContext())
            {
                _context.Playlists.Add(playlist);
                _context.SaveChanges();
            }
        }

        public static void EditPlaylist(string oldPlaylistName, string newPlaylistName)
        {
            using (var _context = new MusicLibraryDbContext())
            {
                var playlist = _context.Playlists.Where(p => p.Name == oldPlaylistName).First();
                playlist.Name = newPlaylistName;
                _context.Playlists.Update(playlist);
                _context.SaveChanges();
            }
        }

        public static void DeletePlaylist(string playlistName)
        {
            using (var _context = new MusicLibraryDbContext())
            {
                var playlist = _context.Playlists.Where(p => p.Name == playlistName).First();
                _context.Playlists.Remove(playlist);
                _context.SaveChanges();
            }
        }

        public static void AddSongsToPlaylist(string playlistName, List<Music> musics)
        {
            using (var _context = new MusicLibraryDbContext())
            {
                var playlist = _context.Playlists.Where(p => p.Name == playlistName).First();
                foreach (var item in musics)
                {
                    var mInP = new MusicInPlaylist
                    {
                        MusicId = item.Id,
                        PlaylistId = playlist.Id
                    };
                    _context.MusicInPlaylists.Add(mInP);
                    _context.SaveChanges();
                }

            }
        }

        public static void RemoveSongsFromPlaylist(string playlistName, List<Music> musics)
        {
            using (var _context = new MusicLibraryDbContext())
            {
                var playlist = _context.Playlists.Where(p => p.Name == playlistName).First();
                foreach (var item in musics)
                {
                    var mInP = _context.MusicInPlaylists.Where(m => m.PlaylistId == playlist.Id && m.MusicId == item.Id).First();
                    _context.MusicInPlaylists.Remove(mInP);
                }
                _context.SaveChanges();
            }
        }

        
        #endregion

    }
}
