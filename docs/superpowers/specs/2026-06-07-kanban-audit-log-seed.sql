-- ============================================================
-- TPS.Nexus.Kanban — Audit Log 測試種子資料
-- Version : v1.0
-- Date    : 2026-06-07
-- ============================================================

SET NAMES utf8mb4;

-- 確保資料表存在
CREATE TABLE IF NOT EXISTS kanban_audit_logs (
    Id          INT           NOT NULL AUTO_INCREMENT,
    EntityType  VARCHAR(50)   NOT NULL,
    EntityId    INT           NOT NULL DEFAULT 0,
    EntityName  VARCHAR(200)  NOT NULL,
    Action      VARCHAR(20)   NOT NULL DEFAULT 'DELETE',
    PerformedBy VARCHAR(200)  NOT NULL,
    PerformedAt DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Details     TEXT          NULL,
    PRIMARY KEY (Id),
    KEY idx_performed_at (PerformedAt),
    KEY idx_entity (EntityType, EntityId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='稽核日誌';

-- 插入示範資料（代表過去 7 天的刪除操作）
INSERT INTO kanban_audit_logs (EntityType, EntityId, EntityName, Action, PerformedBy, PerformedAt, Details) VALUES
('設備',     1, '壓縮機 A',       'DELETE', 'Demo 管理員', DATE_SUB(NOW(), INTERVAL 6 DAY),  '設備編號 EQ-001，已從地圖移除後刪除'),
('設備',     2, '冷卻水塔 B',     'DELETE', 'Demo 管理員', DATE_SUB(NOW(), INTERVAL 5 DAY),  '設備編號 EQ-002'),
('設備',     3, '抽風機 C',       'DELETE', 'Demo 管理員', DATE_SUB(NOW(), INTERVAL 5 DAY),  NULL),
('地圖',     4, '二樓平面圖',     'DELETE', 'Demo 管理員', DATE_SUB(NOW(), INTERVAL 3 DAY),  '已確認無 Widget 使用後刪除'),
('資料來源', 5, 'PLC_Line1_OPC',  'DELETE', 'Demo 管理員', DATE_SUB(NOW(), INTERVAL 2 DAY),  'OPC-UA 資料來源，已停用'),
('圖示',     0, 'icon_pump.png',  'DELETE', 'Demo 管理員', DATE_SUB(NOW(), INTERVAL 1 DAY),  '無設備引用，圖示庫清除'),
('設備',     8, '傳送帶 D',       'DELETE', 'Demo 管理員', NOW(),                            NULL);
