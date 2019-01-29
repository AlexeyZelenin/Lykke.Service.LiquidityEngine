﻿using System;
using JetBrains.Annotations;
using Lykke.AzureStorage.Tables;
using Lykke.AzureStorage.Tables.Entity.Annotation;
using Lykke.AzureStorage.Tables.Entity.ValueTypesMerging;
using Lykke.Service.LiquidityEngine.Domain;

namespace Lykke.Service.LiquidityEngine.AzureRepositories.PnLStopLosses
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    [ValueTypeMergingStrategy(ValueTypeMergingStrategy.UpdateIfDirty)]
    public class PnLStopLossEngineEntity : AzureTableEntity
    {
        private string _assetPairId;
        private string _pnLStopLossGlobalGlobalSettingsId;
        private TimeSpan _interval;
        private decimal _pnLThreshold;
        private decimal _markup;
        private decimal _totalNegativePnL;
        private DateTime _startTime;
        private DateTime _lastTime;

        private PnLStopLossEngineMode _mode;

        public PnLStopLossEngineEntity()
        {
        }

        public PnLStopLossEngineEntity(string partitionKey, string rowKey)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        public string Id { get; set; }

        public string AssetPairId
        {
            get => _assetPairId;
            set
            {
                if (_assetPairId != value)
                {
                    _assetPairId = value;
                    MarkValueTypePropertyAsDirty();
                }
            }
        }

        public string PnLStopLossGlobalSettingsId
        {
            get => _pnLStopLossGlobalGlobalSettingsId;
            set
            {
                if (_pnLStopLossGlobalGlobalSettingsId != value)
                {
                    _pnLStopLossGlobalGlobalSettingsId = value;
                    MarkValueTypePropertyAsDirty();
                }
            }
        }

        public TimeSpan Interval
        {
            get => _interval;
            set
            {
                if (_interval != value)
                {
                    _interval = value;
                    MarkValueTypePropertyAsDirty();
                }
            }
        }

        public decimal PnLThreshold
        {
            get => _pnLThreshold;
            set
            {
                if (_pnLThreshold != value)
                {
                    _pnLThreshold = value;
                    MarkValueTypePropertyAsDirty();
                }
            }
        }

        public decimal Markup
        {
            get => _markup;
            set
            {
                if (_markup != value)
                {
                    _markup = value;
                    MarkValueTypePropertyAsDirty();
                }
            }
        }

        public decimal TotalNegativePnL
        {
            get => _totalNegativePnL;
            set
            {
                if (_totalNegativePnL != value)
                {
                    _totalNegativePnL = value;
                    MarkValueTypePropertyAsDirty();
                }
            }
        }

        public DateTime StartTime
        {
            get => _startTime;
            set
            {
                if (_startTime != value)
                {
                    _startTime = value;
                    MarkValueTypePropertyAsDirty();
                }
            }
        }

        public DateTime LastTime
        {
            get => _lastTime;
            set
            {
                if (_lastTime != value)
                {
                    _lastTime = value;
                    MarkValueTypePropertyAsDirty();
                }
            }
        }

        public PnLStopLossEngineMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    MarkValueTypePropertyAsDirty();
                }
            }
        }
    }
}
