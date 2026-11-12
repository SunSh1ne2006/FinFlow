/* Минималистичные графики на canvas без внешних библиотек. */
const Charts = (() => {
    const palette = ['#1d4ed8', '#0ea5e9', '#94a3b8', '#f59e0b', '#16a34a', '#dc2626'];

    function setup(canvas) {
        const ratio = window.devicePixelRatio || 1;
        const rect = canvas.getBoundingClientRect();
        const width = Math.max(320, rect.width || canvas.parentElement.clientWidth || 600);
        const height = Math.max(180, rect.height || 260);
        canvas.width = Math.round(width * ratio);
        canvas.height = Math.round(height * ratio);
        const ctx = canvas.getContext('2d');
        ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
        ctx.clearRect(0, 0, width, height);
        return { ctx, width, height };
    }

    function compact(value) {
        const abs = Math.abs(value);
        if (abs >= 1e9) return (value / 1e9).toFixed(1).replace('.', ',') + ' млрд';
        if (abs >= 1e6) return (value / 1e6).toFixed(1).replace('.', ',') + ' млн';
        if (abs >= 1e3) return (value / 1e3).toFixed(0) + ' тыс.';
        return String(Math.round(value));
    }

    function niceBounds(values) {
        let min = Math.min(...values);
        let max = Math.max(...values, 0);
        if (!isFinite(min) || !isFinite(max)) {
            min = 0;
            max = 1;
        }
        if (min === max) {
            max = min + 1;
        }
        const pad = (max - min) * 0.12;
        return { min: min - pad, max: max + pad };
    }

    function axes(ctx, box, bounds, labels, ticks) {
        const { width, height } = box;
        const left = 62;
        const right = 14;
        const top = 14;
        const bottom = 30;
        const plotWidth = width - left - right;
        const plotHeight = height - top - bottom;

        ctx.strokeStyle = '#e2e8f0';
        ctx.fillStyle = '#94a3b8';
        ctx.lineWidth = 1;
        ctx.font = '11px "Segoe UI", system-ui, sans-serif';

        for (let i = 0; i <= ticks; i++) {
            const y = top + (plotHeight * i) / ticks;
            ctx.beginPath();
            ctx.moveTo(left, y);
            ctx.lineTo(left + plotWidth, y);
            ctx.stroke();
            const value = bounds.max - ((bounds.max - bounds.min) * i) / ticks;
            ctx.textAlign = 'right';
            ctx.textBaseline = 'middle';
            ctx.fillText(compact(value), left - 8, y);
        }

        if (labels && labels.length) {
            const step = Math.max(1, Math.ceil(labels.length / 7));
            ctx.textAlign = 'center';
            ctx.textBaseline = 'top';
            for (let i = 0; i < labels.length; i += step) {
                const x = left + (plotWidth * i) / Math.max(1, labels.length - 1);
                ctx.fillText(labels[i], x, top + plotHeight + 8);
            }
        }

        return { left, top, plotWidth, plotHeight };
    }

    function lineChart(canvas, options) {
        const box = setup(canvas);
        const { ctx } = box;
        const series = (options.series || []).filter((item) => item.points && item.points.length);
        if (!series.length) {
            ctx.fillStyle = '#94a3b8';
            ctx.font = '13px "Segoe UI", system-ui, sans-serif';
            ctx.fillText('Нет данных для построения графика', 20, 40);
            return;
        }

        const values = series.flatMap((item) => item.points.map((point) => point.y));
        if (options.minimum !== undefined) {
            values.push(options.minimum);
        }
        const bounds = niceBounds(values.concat([0]));
        const area = axes(ctx, box, bounds, options.labels, 4);
        const count = Math.max(1, series[0].points.length - 1);

        series.forEach((item, index) => {
            const color = item.color || palette[index % palette.length];
            const points = item.points.map((point, i) => ({
                x: area.left + (area.plotWidth * i) / Math.max(1, count),
                y: area.top + area.plotHeight * (1 - (point.y - bounds.min) / (bounds.max - bounds.min))
            }));

            if (item.fill) {
                ctx.beginPath();
                ctx.moveTo(points[0].x, area.top + area.plotHeight);
                points.forEach((point) => ctx.lineTo(point.x, point.y));
                ctx.lineTo(points[points.length - 1].x, area.top + area.plotHeight);
                ctx.closePath();
                ctx.fillStyle = item.fill;
                ctx.fill();
            }

            ctx.beginPath();
            points.forEach((point, i) => (i === 0 ? ctx.moveTo(point.x, point.y) : ctx.lineTo(point.x, point.y)));
            ctx.strokeStyle = color;
            ctx.lineWidth = item.dashed ? 1.6 : 2.2;
            if (item.dashed) {
                ctx.setLineDash([5, 4]);
            }
            ctx.stroke();
            ctx.setLineDash([]);

            if (item.highlightGaps) {
                ctx.fillStyle = 'rgba(220, 38, 38, .16)';
                points.forEach((point, i) => {
                    if (item.points[i].y > options.gapThreshold) {
                        ctx.fillRect(point.x - area.plotWidth / (count * 2), area.top, Math.max(2, area.plotWidth / count), area.plotHeight);
                    }
                });
            }
        });

        if (options.minimum !== undefined) {
            const y = area.top + area.plotHeight * (1 - (options.minimum - bounds.min) / (bounds.max - bounds.min));
            ctx.beginPath();
            ctx.moveTo(area.left, y);
            ctx.lineTo(area.left + area.plotWidth, y);
            ctx.strokeStyle = '#f59e0b';
            ctx.setLineDash([6, 4]);
            ctx.lineWidth = 1.5;
            ctx.stroke();
            ctx.setLineDash([]);
            ctx.fillStyle = '#b45309';
            ctx.font = '11px "Segoe UI", system-ui, sans-serif';
            ctx.textAlign = 'left';
            ctx.fillText(options.minimumLabel || 'Минимальный запас', area.left + 6, y - 6);
        }
    }

    function barChart(canvas, options) {
        const box = setup(canvas);
        const { ctx } = box;
        const items = (options.items || []).slice(0, 10);
        if (!items.length) {
            ctx.fillStyle = '#94a3b8';
            ctx.font = '13px "Segoe UI", system-ui, sans-serif';
            ctx.fillText('Нет данных для построения диаграммы', 20, 40);
            return;
        }

        const left = 12;
        const right = 74;
        const top = 10;
        const rowHeight = 26;
        const width = box.width - left - right;
        const max = Math.max(...items.map((item) => Math.abs(item.value)), 1);
        const labelWidth = Math.min(190, Math.max(96, box.width * 0.34));

        items.forEach((item, index) => {
            const y = top + index * rowHeight;
            const barWidth = Math.max(2, (width - labelWidth) * (Math.abs(item.value) / max));

            ctx.fillStyle = '#334155';
            ctx.font = '11.5px "Segoe UI", system-ui, sans-serif';
            ctx.textAlign = 'right';
            ctx.textBaseline = 'middle';
            ctx.fillText(trim(item.label, 30), left + labelWidth - 10, y + rowHeight / 2);

            ctx.fillStyle = item.color || palette[index % palette.length];
            ctx.beginPath();
            const radius = 4;
            const x = left + labelWidth;
            const h = 12;
            const barY = y + (rowHeight - h) / 2;
            ctx.roundRect(x, barY, barWidth, h, radius);
            ctx.fill();

            ctx.fillStyle = '#475569';
            ctx.textAlign = 'left';
            ctx.fillText(options.formatter ? options.formatter(item.value) : compact(item.value), x + barWidth + 8, y + rowHeight / 2);
        });
    }

    function trim(text, limit) {
        const value = String(text || '');
        return value.length > limit ? value.slice(0, limit - 1) + '…' : value;
    }

    return { lineChart, barChart, compact };
})();
