use protobuf::well_known_types::{Struct as ProtoStruct, Value};

use std::collections::HashMap;
use std::convert::TryFrom;
use std::error::Error;
use std::fmt::{Display, Formatter};
use std::rc::Rc;
use std::time::Duration;

#[derive(Debug, Clone)]
pub struct Configuration {
    /// running in gateway or sidecar?
    pub is_gw: bool,

    /// Cache duration in seconds
    pub cache_refresh_seconds: Duration,

    /// Name of this deployment, if not running as gw, ignored otherwise
    pub deployment: Rc<String>,

    /// Namespace of this app, if not running as gw, ignored otherwise
    pub namespace: Rc<String>,

    /// The authority to set when calling the HTTP service providing headers.
    pub service_authority: Rc<String>,

    /// The Envoy cluster name
    pub service_cluster: Rc<String>,

    /// The path to call on the HTTP service providing headers.
    pub service_path: Rc<String>,

    /// user agent
    pub user_agent: Rc<String>,
}

#[derive(Debug)]
pub enum ConfigurationError {
    Missing,
    Format(String),
}

impl Display for ConfigurationError {
    fn fmt(&self, f: &mut Formatter<'_>) -> std::fmt::Result {
        match self {
            ConfigurationError::Missing => {
                write!(f, "Configuration Missing")
            }
            ConfigurationError::Format(s) => {
                write!(f, "Format: {}", s.as_str())
            }
        }
    }
}

impl Error for ConfigurationError {}

impl TryFrom<ProtoStruct> for Configuration {
    type Error = ConfigurationError;

    fn try_from(mut value: ProtoStruct) -> Result<Self, Self::Error> {
        let mut fields = value.take_fields();

        let is_gw = take_bool_field(&mut fields, "is_gw")?;
        let service_cluster = take_string_field(&mut fields, "service_cluster")?;
        let service_path = take_string_field(&mut fields, "service_path")?;

        let user_agent = take_string_field(&mut fields, "user_agent")?;
        let service_authority = take_string_field(&mut fields, "service_authority")?;

        let cache_duration = take_duration_field(&mut fields, "cache_refresh_seconds")?
            .unwrap_or(Duration::from_secs(30));
        let ns: String;
        let deployment: String;
        if !is_gw {
            ns = take_string_field(&mut fields, "namespace")?;
            deployment = take_string_field(&mut fields, "deployment")?;
        } else {
            ns = String::new();
            deployment = String::new();
        }

        Ok(Configuration {
            is_gw: is_gw,
            cache_refresh_seconds: cache_duration,
            deployment: Rc::new(deployment),
            namespace: Rc::new(ns),
            service_authority: Rc::new(service_authority),
            service_cluster: Rc::new(service_cluster),
            service_path: Rc::new(service_path),
            user_agent: Rc::new(user_agent),
        })
    }
}

fn take_string_field(
    fields: &mut HashMap<String, Value>,
    key: &str,
) -> Result<String, ConfigurationError> {
    fields
        .remove(key)
        .ok_or_else(|| ConfigurationError::Format(format!("{} is missing", key)))
        .and_then(|mut v| {
            if v.has_string_value() {
                Ok(v.take_string_value())
            } else {
                Err(ConfigurationError::Format(format!(
                    "{} is not a string",
                    key
                )))
            }
        })
}

fn take_bool_field(
    fields: &mut HashMap<String, Value>,
    key: &str,
) -> Result<bool, ConfigurationError> {
    fields
        .remove(key)
        .ok_or_else(|| ConfigurationError::Format(format!("{} is missing", key)))
        .and_then(|v| {
            if v.has_bool_value() {
                Ok(v.get_bool_value())
            } else {
                Err(ConfigurationError::Format(format!("{} is not a bool", key)))
            }
        })
}

fn take_duration_field(
    fields: &mut HashMap<String, Value>,
    key: &str,
) -> Result<Option<Duration>, ConfigurationError> {
    let v = fields.remove(key).map(|v| {
        if v.has_number_value() {
            Ok(v.get_number_value())
        } else {
            Err(ConfigurationError::Format(format!(
                "{} is not a duration",
                key
            )))
        }
    });

    match v {
        None => Ok(None),
        Some(Ok(d)) if d.is_sign_negative() => Err(ConfigurationError::Format(format!(
            "{} is not a duration, since it has a negative value",
            key
        ))),
        Some(Ok(d)) => Ok(Some(Duration::from_secs(d as u64))),
        Some(Err(e)) => Err(e),
    }
}
