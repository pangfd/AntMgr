﻿using AntData.ORM.Data;
using AntData.ORM.Mapping;
using Autofac.Annotation;
using DbModel;
using Infrastructure.CodeGen;
using Infrastructure.Logging;
using Newtonsoft.Json;
using Repository.Interface;
using ServicesModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Threading.Tasks;
using Autofac.Aspect;
using Configuration;
using Infrastructure.StaticExt;
using Repository.Interceptors;
using ViewModels.Reuqest;

namespace Repository
{
    /// <summary>
    /// 公共处理
    /// </summary>
    [Component]
    public class CommonRespository : BaseRepository, ICommonRespository
    {

        private static string _dbTableAndColumnsCache = string.Empty;
        private static List<CodeGenTable> _dbTableCache = null;


        #region SQL

        /// <summary>
        /// 执行sql语句返回DataTable
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>

        public DataTable SelectSqlExcute(string sql)
        {
            if (string.IsNullOrEmpty(sql))
            {
                return new DataTable();
            }
            return this.DB.QueryTable(sql);
        }

        /// <summary>
        /// 执行sql语句返回受影响条数
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public Tuple<int, string> SQLExcute(string sql)
        {
            int result = -1;
            if (string.IsNullOrEmpty(sql))
            {
                return new Tuple<int, string>(-1, Tip.BadRequest);
            }

            try
            {
                this.DB.UseTransaction(con =>
                {
                    result = con.Execute(sql);
                    return true;
                });

            }
            catch (Exception ex)
            {
                return new Tuple<int, string>(-1, ex.Message);
            }
            if (result == -1)
            {
                return new Tuple<int, string>(result, "请使用Select按钮查询！");
            }
            return new Tuple<int, string>(result, string.Empty);
        }

        #endregion

        /// <summary>
        /// 获取所有的Table和Columns
        /// </summary>
        /// <returns></returns>
        public string GetDbTablesAndColumns()
        {
            if (!string.IsNullOrEmpty(_dbTableAndColumnsCache)) return _dbTableAndColumnsCache;
            Dictionary<string, List<string>> result = new Dictionary<string, List<string>>();
            List<string> tables = this.DB.Query<string>("show tables").ToList();
            foreach (var table in tables)
            {
                var columns = getAllFields(table);
                result.Add(table, columns);
            }
            _dbTableAndColumnsCache = JsonConvert.SerializeObject(result);
            return _dbTableAndColumnsCache;
        }

        /// <summary>
        /// 获取所有的表
        /// </summary>
        /// <returns></returns>
        public List<CodeGenTable> GetDbTables()
        {
            if (_dbTableCache != null)
            {
                return _dbTableCache;
            }
            _dbTableCache = this.GetDbTabless();
            return _dbTableCache;
        }

        /// <summary>
        /// 获取表下面所有的字段
        /// </summary>
        /// <param name="dbName"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public List<CodeGenField> GetDbTablesColumns(string dbName, string tableName)
        {
            var key = (string.IsNullOrEmpty(dbName) ? "" : dbName + ".") + tableName;
            if (_dbColumnsCache.TryGetValue(key, out var cache)) return cache;
            cache = this.GetDbModels(dbName, tableName);
            _dbColumnsCache.TryAdd(key, cache);
            return cache;
        }

        /// <summary>
        /// 自动生成代码
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public byte[] CodeGen(CodeGenVm model)
        {
            return GeneratorCodeHelper.CodeGenerator(model.TableName, model.Columns);
        }


        /// <summary>
        /// 获取表里面所有的字段
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private List<string> getAllFields(string tableName)
        {
            var columns = this.DB.Query<string>(" SHOW COLUMNS FROM " + tableName).ToList();
            return columns;
        }


        /// <summary>
        /// 获取所有的DBTable
        /// </summary>
        private List<CodeGenTable> GetDbTabless()
        {
            var result = new List<CodeGenTable>();
            try
            {

                var modelAss = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => (assembly.GetName().Name.Equals("DbModel")));
                if (modelAss == null)
                {
                    throw new ArgumentException("assemblys");
                }
                var types = modelAss.GetExportedTypes();
                var targetClass = (from t in types
                                   where t.BaseType == typeof(LinqToDBEntity) &&
                                       !t.IsAbstract &&
                                       !t.IsInterface
                                   select t).ToArray();


                foreach (var tt in targetClass)
                {
                    var tart = tt.GetCustomAttribute<TableAttribute>();
                    if (tart == null)
                    {
                        continue;
                    }

                    var comment = tart.Comment;
                    if (string.IsNullOrEmpty(comment))
                    {
                        comment = string.Empty;
                        LogHelper.Debug("GetDbTabless", tart.Name + "表的Comment为空!!");
                    }
                    result.Add(new CodeGenTable
                    {
                        DbName = tart.Db,
                        Name = tt.Name,
                        TableName = tart.Name,
                        Comment = comment.Replace(",", "").Replace("→", "")
                    });
                }
                return result.OrderBy(r => r.Name).ToList();
            }
            catch (Exception ex)
            {

                LogHelper.Warn("GetDbTabless", "可能有表的Comment为空导致", ex);
            }
            return result;
        }




    }
}
