﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Service.LiquidityEngine.Domain;
using Lykke.Service.LiquidityEngine.Domain.Exceptions;
using Lykke.Service.LiquidityEngine.Domain.Extensions;
using Lykke.Service.LiquidityEngine.Domain.Repositories;
using Lykke.Service.LiquidityEngine.Domain.Services;

namespace Lykke.Service.LiquidityEngine.DomainServices.PnLStopLossEngines
{
    public class PnLStopLossEngineService : IPnLStopLossEngineService
    {
        private readonly IPnLStopLossEngineRepository _pnLStopLossEngineRepository;
        private readonly InMemoryCache<PnLStopLossEngine> _cache;
        private readonly IInstrumentService _instrumentService;
        private readonly ICrossRateInstrumentService _crossRateInstrumentService;
        private readonly ILog _log;

        public PnLStopLossEngineService(
            IPnLStopLossEngineRepository pnLStopLossEngineRepository,
            IInstrumentService instrumentService,
            ICrossRateInstrumentService crossRateInstrumentService,
            ILogFactory logFactory)
        {
            _pnLStopLossEngineRepository = pnLStopLossEngineRepository;
            _cache = new InMemoryCache<PnLStopLossEngine>(engine => engine.Id, false);
            _instrumentService = instrumentService;
            _crossRateInstrumentService = crossRateInstrumentService;
            _log = logFactory.CreateLog(this);
        }

        public async Task<IReadOnlyCollection<PnLStopLossEngine>> GetAllAsync()
        {
            IReadOnlyCollection<PnLStopLossEngine> pnLStopLossEngines = _cache.GetAll();

            if (pnLStopLossEngines == null)
                pnLStopLossEngines = await Initialize();

            return pnLStopLossEngines;
        }

        public async Task AddAsync(PnLStopLossEngine pnLStopLossEngine)
        {
            if (!string.IsNullOrWhiteSpace(pnLStopLossEngine.Id))
                throw new InvalidOperationException("PnL stop loss engine already has an identifier.");

            if (await _instrumentService.TryGetByAssetPairIdAsync(pnLStopLossEngine.AssetPairId) == null)
                throw new InvalidOperationException($"Asset pair id is not found: '{pnLStopLossEngine.AssetPairId}'.");

            if (string.IsNullOrWhiteSpace(pnLStopLossEngine.AssetPairId))
                throw new InvalidOperationException("PnL stop loss engine must contain asset pair id.");

            pnLStopLossEngine.Id = Guid.NewGuid().ToString();

            pnLStopLossEngine.Mode = PnLStopLossEngineMode.Idle;

            _log.InfoWithDetails("Creating pnl stop loss engine.", pnLStopLossEngine);

            await _pnLStopLossEngineRepository.InsertAsync(pnLStopLossEngine);

            _cache.Set(pnLStopLossEngine);

            _log.InfoWithDetails("PnL stop loss engine created.", pnLStopLossEngine);
        }

        public async Task UpdateAsync(PnLStopLossEngine pnLStopLossEngine)
        {
            if (string.IsNullOrWhiteSpace(pnLStopLossEngine.Id))
                throw new InvalidOperationException("PnL stop loss engine must have an identifier.");

            PnLStopLossEngine currentPnLStopLossEngine = await GetEngineByIdAsync(pnLStopLossEngine.Id);

            currentPnLStopLossEngine.Update(pnLStopLossEngine);

            await _pnLStopLossEngineRepository.UpdateAsync(currentPnLStopLossEngine);

            _cache.Set(currentPnLStopLossEngine);

            _log.InfoWithDetails("PnL stop loss engine updated.", currentPnLStopLossEngine);
        }

        public async Task DisableAsync(string id)
        {
            var pnLStopLossEngine = await GetEngineByIdAsync(id);

            pnLStopLossEngine.Disable();

            await UpdateAsync(pnLStopLossEngine);
        }

        public async Task EnableAsync(string id)
        {
            var pnLStopLossEngine = await GetEngineByIdAsync(id);

            pnLStopLossEngine.Enable();

            await UpdateAsync(pnLStopLossEngine);
        }

        public async Task DeleteAsync(string id)
        {
            await GetEngineByIdAsync(id);

            await _pnLStopLossEngineRepository.DeleteAsync(id);

            _cache.Remove(id);

            _log.InfoWithDetails("PnL stop loss engine deleted.", id);
        }

        public async Task<decimal> GetTotalMarkupByAssetPairIdAsync(string assetPairId)
        {
            IReadOnlyCollection<PnLStopLossEngine> engines = await GetAllAsync();

            engines = engines.Where(x => x.AssetPairId == assetPairId
                                         && x.Mode == PnLStopLossEngineMode.Active
                                         && x.TotalNegativePnL <= x.Threshold).ToList();

            decimal totalMarkup = engines.Sum(x => x.Markup);

            return totalMarkup;
        }

        public async Task<IReadOnlyCollection<AssetPairMarkup>> GetTotalMarkups()
        {
            var result = new List<AssetPairMarkup>();
            
            IReadOnlyCollection<Instrument> assetPairs = await _instrumentService.GetAllAsync();

            IReadOnlyCollection<PnLStopLossEngine> engines = (await GetAllAsync())
                .Where(x => x.Mode == PnLStopLossEngineMode.Active
                         && x.TotalNegativePnL <= x.Threshold).ToList();

            foreach (var assetPair in assetPairs)
            {
                IReadOnlyCollection<PnLStopLossEngine> assetPairEngines = engines.Where(x => x.AssetPairId == assetPair.AssetPairId).ToList();
                decimal sum = assetPairEngines.Sum(x => x.Markup);

                result.Add(new AssetPairMarkup
                {
                    AssetPairId = assetPair.AssetPairId,
                    TotalMarkup = sum,
                    TotalAskMarkup = sum,
                    TotalBidMarkup = sum
                });
            }

            return result;
        }

        public async Task ExecuteAsync()
        {
            IReadOnlyCollection<PnLStopLossEngine> allPnLStopLossEngines = await GetAllAsync();

            foreach (var pnLStopLossEngine in allPnLStopLossEngines)
                await Refresh(pnLStopLossEngine);
        }

        public async Task HandleClosedPositionAsync(Position position)
        {
            if (position.PnL >= 0)
                return;

            _log.InfoWithDetails("Received position with negative PnL.", position);

            IReadOnlyCollection<PnLStopLossEngine> pnLStopLossEngines = await GetEnabledEnginesByAssetPairIdAsync(position.AssetPairId);

            if (!pnLStopLossEngines.Any())
                return;

            decimal? pnlUsd = await _crossRateInstrumentService.ConvertPriceAsync(position.AssetPairId, position.PnL);

            if (!pnlUsd.HasValue || pnlUsd.Value == 0)
            {
                string pnlUsdStr = pnlUsd.HasValue ? pnlUsd.Value.ToString(CultureInfo.InvariantCulture) : "null";
                _log.WarningWithDetails($"PnL converted to USD is '{pnlUsdStr}' for '{position.AssetPairId}'. No cross instrument or quote.", position);

                pnlUsd = pnlUsd ?? 0;
            }

            foreach (PnLStopLossEngine pnLStopLossEngine in pnLStopLossEngines)
            {
                pnLStopLossEngine.AddNegativePnL(pnlUsd.Value);

                await UpdateAsync(pnLStopLossEngine);
            }

            if (pnLStopLossEngines.Any())
                _log.InfoWithDetails("Applied position to pnl stop loss engines: " +
                                     $"{string.Join(", ", pnLStopLossEngines.Select(x => x.AssetPairId).ToList())}.", position);
        }

        private async Task<IReadOnlyCollection<PnLStopLossEngine>> Initialize()
        {
            IReadOnlyCollection<PnLStopLossEngine> pnLStopLossEngines = await _pnLStopLossEngineRepository.GetAllAsync();

            _cache.Initialize(pnLStopLossEngines);

            return pnLStopLossEngines;
        }

        private async Task Refresh(PnLStopLossEngine pnLStopLossEngine)
        {
            var isUpdated = pnLStopLossEngine.Refresh();

            if (isUpdated)
                await UpdateAsync(pnLStopLossEngine);
        }

        private async Task<IReadOnlyCollection<PnLStopLossEngine>> GetEnabledEnginesByAssetPairIdAsync(string assetPairId)
        {
            IReadOnlyCollection<PnLStopLossEngine> pnLStopLossEngines = await GetAllAsync();

            pnLStopLossEngines = pnLStopLossEngines
                .Where(x => x.AssetPairId == assetPairId)
                .Where(x => x.Mode != PnLStopLossEngineMode.Disabled)
                .ToList();

            return pnLStopLossEngines;
        }

        private async Task<PnLStopLossEngine> GetEngineByIdAsync(string id)
        {
            IReadOnlyCollection<PnLStopLossEngine> pnLStopLossEngines = await GetAllAsync();

            PnLStopLossEngine pnLStopLossSettings = pnLStopLossEngines.FirstOrDefault(o => o.Id == id);

            if (pnLStopLossSettings == null)
                throw new EntityNotFoundException();

            return pnLStopLossSettings;
        }
    }
}
