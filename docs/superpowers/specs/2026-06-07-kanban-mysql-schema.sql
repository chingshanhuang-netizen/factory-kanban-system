-- ============================================================
-- TPS.Nexus.Kanban — MySQL 8.0 Database Schema
-- Version : v1.3
-- Date    : 2026-06-07
-- Engine  : InnoDB, utf8mb4_unicode_ci
-- ============================================================
-- 執行順序依 FK 依賴：無 FK 的表先建，有 FK 的表後建
-- 移除舊版請先執行底部的 DROP 區段（順序相反）
-- ============================================================

-- ------------------------------------------------------------
-- 0. 資料庫設定（可選，若由 DBA 統一管理可略過）
-- ------------------------------------------------------------
-- CREATE DATABASE IF NOT EXISTS tps_nexus
--   CHARACTER SET utf8mb4
--   COLLATE utf8mb4_unicode_ci;
-- USE tps_nexus;

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ============================================================
-- 1. kanban_factory_maps
--    工廠平面圖定義（含輪播設定）
-- ============================================================
CREATE TABLE IF NOT EXISTS kanban_factory_maps (
    Id              INT             NOT NULL AUTO_INCREMENT,
    Name            VARCHAR(200)    NOT NULL                    COMMENT '地圖顯示名稱',
    FormatType      TINYINT         NOT NULL                    COMMENT '0=PNG 1=JPG 2=SVG 3=DXF 4=JsonCoord 5=XmlCoord',
    FilePath        VARCHAR(500)    NOT NULL                    COMMENT '儲存路徑或 URL',
    ThumbnailPath   VARCHAR(500)    NULL                        COMMENT '縮圖路徑（可空）',
    CreatedAt       DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '建立時間',
    Floor           VARCHAR(50)     NULL                        COMMENT '樓層（如 1F / 2F）',
    Area            VARCHAR(100)    NULL                        COMMENT '廠區名稱',
    Version         VARCHAR(50)     NOT NULL DEFAULT 'v1'       COMMENT '人工版本標籤（顯示於設定管理）',
    CarouselEnabled TINYINT(1)      NOT NULL DEFAULT 0          COMMENT '是否加入輪播序列',
    CarouselSeconds INT             NOT NULL DEFAULT 10         COMMENT '輪播停留秒數',
    CarouselOrder   INT             NOT NULL DEFAULT 0          COMMENT '輪播排序（升冪）',

    PRIMARY KEY (Id),
    KEY idx_carousel (CarouselEnabled, CarouselOrder)           COMMENT '輪播排序查詢'
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci
  COMMENT='工廠平面圖';


-- ============================================================
-- 2. kanban_layout_versions
--    版面版本歷程（Draft / Published / Archived）
-- ============================================================
CREATE TABLE IF NOT EXISTS kanban_layout_versions (
    Id              INT             NOT NULL AUTO_INCREMENT,
    FactoryMapId    INT             NOT NULL                    COMMENT 'FK → kanban_factory_maps.Id',
    VersionNo       INT             NOT NULL                    COMMENT '版本序號（同 MapId 遞增）',
    Status          TINYINT         NOT NULL DEFAULT 0          COMMENT '0=Draft 1=Published 2=Archived',
    CreatedBy       VARCHAR(100)    NOT NULL                    COMMENT '建立人帳號',
    PublishedAt     DATETIME        NULL                        COMMENT '發布時間',
    LayoutJson      LONGTEXT        NULL                        COMMENT '版面 JSON 快照',

    PRIMARY KEY (Id),
    KEY idx_map_status  (FactoryMapId, Status)                  COMMENT '查詢指定地圖已發布版本',
    KEY idx_map_version (FactoryMapId, VersionNo),
    CONSTRAINT fk_lv_map FOREIGN KEY (FactoryMapId)
        REFERENCES kanban_factory_maps (Id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci
  COMMENT='版面版本歷程';


-- ============================================================
-- 3. kanban_equipment
--    設備主檔
-- ============================================================
CREATE TABLE IF NOT EXISTS kanban_equipment (
    Id              INT             NOT NULL AUTO_INCREMENT,
    Name            VARCHAR(200)    NOT NULL                    COMMENT '設備名稱',
    Category        VARCHAR(100)    NULL                        COMMENT '設備類別（如加工設備/物流設備）',
    MapName         VARCHAR(200)    NULL                        COMMENT '所屬地圖名稱（冗餘欄，方便篩選）',
    Tag             VARCHAR(100)    NULL                        COMMENT '設備標籤／編號',
    Description     TEXT            NULL                        COMMENT '描述',
    IconType        TINYINT         NOT NULL DEFAULT 0          COMMENT '0=CssClass 1=CustomImage',
    IconValue       VARCHAR(500)    NULL                        COMMENT 'CSS class 名稱或圖示路徑',

    PRIMARY KEY (Id),
    KEY idx_name (Name)
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci
  COMMENT='設備主檔';


-- ============================================================
-- 4. kanban_datasource_configs
--    資料來源設定（SQL / CSV / JSON / XML）
-- ============================================================
CREATE TABLE IF NOT EXISTS kanban_datasource_configs (
    Id                INT             NOT NULL AUTO_INCREMENT,
    Name              VARCHAR(200)    NOT NULL                    COMMENT '設定名稱',
    SourceType        TINYINT         NOT NULL                    COMMENT '0=SQL 1=CSV 2=JSON 3=XML',
    DataType          VARCHAR(50)     NULL                        COMMENT '資料類型標籤（如 溫度/電流/計數）',
    ConnectionString  TEXT            NULL                        COMMENT 'SQL 連線字串（加密儲存建議）',
    FilePath          VARCHAR(500)    NULL                        COMMENT '檔案來源路徑',
    QueryOrPath       TEXT            NULL                        COMMENT 'SQL 語句 或 JSON/XML 路徑',
    Parameters        TEXT            NULL                        COMMENT 'JSON 格式參數（如 {"id":1}）',

    PRIMARY KEY (Id),
    KEY idx_source_type (SourceType)
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci
  COMMENT='資料來源設定';


-- ============================================================
-- 5. kanban_equipment_widgets
--    設備 Widget 在版面上的位置與大小
-- ============================================================
CREATE TABLE IF NOT EXISTS kanban_equipment_widgets (
    Id                INT     NOT NULL AUTO_INCREMENT,
    EquipmentId       INT     NOT NULL                    COMMENT 'FK → kanban_equipment.Id',
    LayoutVersionId   INT     NOT NULL                    COMMENT 'FK → kanban_layout_versions.Id',
    PositionX         INT     NOT NULL DEFAULT 0          COMMENT 'px，相對地圖左上角',
    PositionY         INT     NOT NULL DEFAULT 0          COMMENT 'px，相對地圖左上角',
    Width             INT     NOT NULL DEFAULT 80         COMMENT 'px',
    Height            INT     NOT NULL DEFAULT 100        COMMENT 'px',

    PRIMARY KEY (Id),
    KEY idx_version     (LayoutVersionId)                 COMMENT '批次載入版面所有 widget',
    KEY idx_equipment   (EquipmentId),
    CONSTRAINT fk_ew_equipment FOREIGN KEY (EquipmentId)
        REFERENCES kanban_equipment (Id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,
    CONSTRAINT fk_ew_version FOREIGN KEY (LayoutVersionId)
        REFERENCES kanban_layout_versions (Id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci
  COMMENT='設備 Widget 位置';


-- ============================================================
-- 6. kanban_widget_components
--    Widget 內部子元件（狀態燈 / 數值 / 趨勢圖）
-- ============================================================
CREATE TABLE IF NOT EXISTS kanban_widget_components (
    Id                  INT             NOT NULL AUTO_INCREMENT,
    EquipmentWidgetId   INT             NOT NULL                    COMMENT 'FK → kanban_equipment_widgets.Id',
    ComponentType       TINYINT         NOT NULL                    COMMENT '0=StatusIndicator 1=ValueGauge 2=TrendChart',
    DataSourceConfigId  INT             NULL                        COMMENT 'FK → kanban_datasource_configs.Id（可空）',
    Label               VARCHAR(200)    NULL                        COMMENT '顯示標籤',
    Unit                VARCHAR(50)     NULL                        COMMENT '單位（如 rpm / °C）',
    RefreshInterval     INT             NOT NULL DEFAULT 30         COMMENT '更新間隔（秒）',
    DisplayOrder        INT             NOT NULL DEFAULT 0          COMMENT '同 Widget 內排序',
    ConfigJson          TEXT            NULL                        COMMENT '元件額外設定 JSON',

    PRIMARY KEY (Id),
    KEY idx_widget      (EquipmentWidgetId),
    KEY idx_datasource  (DataSourceConfigId),
    CONSTRAINT fk_wc_widget FOREIGN KEY (EquipmentWidgetId)
        REFERENCES kanban_equipment_widgets (Id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,
    CONSTRAINT fk_wc_datasource FOREIGN KEY (DataSourceConfigId)
        REFERENCES kanban_datasource_configs (Id)
        ON DELETE SET NULL
        ON UPDATE CASCADE
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci
  COMMENT='Widget 子元件';


-- ============================================================
-- 7. kanban_alarm_rules
--    警報規則（每條規則對應一個設備 + 一個資料來源）
-- ============================================================
CREATE TABLE IF NOT EXISTS kanban_alarm_rules (
    Id                  INT             NOT NULL AUTO_INCREMENT,
    EquipmentId         INT             NOT NULL                    COMMENT 'FK → kanban_equipment.Id',
    DataSourceConfigId  INT             NOT NULL                    COMMENT 'FK → kanban_datasource_configs.Id',
    FieldName           VARCHAR(100)    NOT NULL DEFAULT 'value'    COMMENT '監控的欄位名稱',
    `Condition`         VARCHAR(10)     NOT NULL                    COMMENT '條件運算子：> < >= <= == !=',
    Threshold           DOUBLE          NOT NULL                    COMMENT '閾值',
    AlarmLevel          TINYINT         NOT NULL                    COMMENT '0=Info 1=Warning 2=Critical',
    Message             VARCHAR(500)    NULL                        COMMENT '觸發時的警報訊息範本',
    IsEnabled           TINYINT(1)      NOT NULL DEFAULT 1          COMMENT '是否啟用',

    PRIMARY KEY (Id),
    KEY idx_equipment   (EquipmentId),
    CONSTRAINT fk_ar_equipment FOREIGN KEY (EquipmentId)
        REFERENCES kanban_equipment (Id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,
    CONSTRAINT fk_ar_datasource FOREIGN KEY (DataSourceConfigId)
        REFERENCES kanban_datasource_configs (Id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci
  COMMENT='警報規則';


-- ============================================================
-- 8. kanban_alarm_records
--    警報歷史紀錄（SignalR 推播時寫入）
-- ============================================================
CREATE TABLE IF NOT EXISTS kanban_alarm_records (
    Id              INT             NOT NULL AUTO_INCREMENT,
    EquipmentId     INT             NOT NULL                    COMMENT '冗餘欄，方便查詢（設備可能已刪除）',
    EquipmentName   VARCHAR(200)    NOT NULL                    COMMENT '設備名稱快照',
    AlarmRuleId     INT             NULL                        COMMENT 'FK → kanban_alarm_rules.Id（可空，手動觸發時為 NULL）',
    Level           TINYINT         NOT NULL                    COMMENT '0=Info 1=Warning 2=Critical',
    Message         TEXT            NOT NULL                    COMMENT '警報訊息',
    TriggeredAt     DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '觸發時間',
    ResolvedAt      DATETIME        NULL                        COMMENT '解除時間（NULL=仍活躍）',

    PRIMARY KEY (Id),
    KEY idx_active      (EquipmentId, ResolvedAt)               COMMENT '查詢活躍警報',
    KEY idx_triggered   (TriggeredAt),
    KEY idx_level       (Level),
    CONSTRAINT fk_rec_rule FOREIGN KEY (AlarmRuleId)
        REFERENCES kanban_alarm_rules (Id)
        ON DELETE SET NULL
        ON UPDATE CASCADE
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci
  COMMENT='警報歷史紀錄';


-- ============================================================
-- 9. kanban_equipment_link_configs
--    設備詳情抽屜的頁籤連結設定
-- ============================================================
CREATE TABLE IF NOT EXISTS kanban_equipment_link_configs (
    Id                  INT             NOT NULL AUTO_INCREMENT,
    EquipmentId         INT             NOT NULL                    COMMENT 'FK → kanban_equipment.Id',
    LinkType            TINYINT         NOT NULL                    COMMENT '0=WorkOrder 1=AlarmHistory 2=Document 3=CustomUrl',
    TabLabel            VARCHAR(100)    NOT NULL                    COMMENT '頁籤標題',
    UrlTemplate         VARCHAR(1000)   NULL                        COMMENT 'URL 範本（支援 {equipmentId} 佔位符）',
    DataSourceConfigId  INT             NULL                        COMMENT 'FK → kanban_datasource_configs.Id（嵌入資料頁籤用）',
    DisplayOrder        INT             NOT NULL DEFAULT 0          COMMENT '同設備頁籤排序',

    PRIMARY KEY (Id),
    KEY idx_equipment   (EquipmentId, DisplayOrder),
    CONSTRAINT fk_lc_equipment FOREIGN KEY (EquipmentId)
        REFERENCES kanban_equipment (Id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,
    CONSTRAINT fk_lc_datasource FOREIGN KEY (DataSourceConfigId)
        REFERENCES kanban_datasource_configs (Id)
        ON DELETE SET NULL
        ON UPDATE CASCADE
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci
  COMMENT='設備詳情抽屜頁籤設定';


SET FOREIGN_KEY_CHECKS = 1;

-- ============================================================
-- 清除腳本（依 FK 逆序，需要時解除注解執行）
-- ============================================================
-- SET FOREIGN_KEY_CHECKS = 0;
-- DROP TABLE IF EXISTS kanban_equipment_link_configs;
-- DROP TABLE IF EXISTS kanban_alarm_records;
-- DROP TABLE IF EXISTS kanban_alarm_rules;
-- DROP TABLE IF EXISTS kanban_widget_components;
-- DROP TABLE IF EXISTS kanban_equipment_widgets;
-- DROP TABLE IF EXISTS kanban_datasource_configs;
-- DROP TABLE IF EXISTS kanban_equipment;
-- DROP TABLE IF EXISTS kanban_layout_versions;
-- DROP TABLE IF EXISTS kanban_factory_maps;
-- SET FOREIGN_KEY_CHECKS = 1;

-- ============================================================
-- 初始化測試資料（可選）
-- ============================================================
-- INSERT INTO kanban_factory_maps (Name, FormatType, FilePath, Version, CarouselEnabled, CarouselSeconds, CarouselOrder)
-- VALUES
--     ('1F 生產線',  0, '/module-assets/TPS.Nexus.Kanban/maps/floor1.png', 'v1', 1, 15, 1),
--     ('2F 組裝區',  0, '/module-assets/TPS.Nexus.Kanban/maps/floor2.png', 'v1', 1, 10, 2),
--     ('3F 倉儲區',  0, '/module-assets/TPS.Nexus.Kanban/maps/floor3.png', 'v1', 0, 10, 3);
