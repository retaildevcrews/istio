use protobuf::well_known_types::{Struct as ProtoStruct, Value};

use std::collections::HashMap;
use std::convert::TryFrom;
use std::error::Error;
use std::fmt::{Display, Formatter};
use std::rc::Rc;
use std::time::Duration;

#[derive(Debug)]
pub struct FilterConfig {
    /// constant header, set at configmap
    pub burst_header: Rc<String>,

    /// Cache duration in seconds
    pub cache_seconds: Duration,

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
pub enum FilterConfigError {
    Missing,
    Format(String),
}

impl Display for FilterConfigError {
    fn fmt(&self, f: &mut Formatter<'_>) -> std::fmt::Result {
        match self {
            FilterConfigError::Missing => {
                write!(f, "Filter Config Missing")
            }
            FilterConfigError::Format(s) => {
                write!(f, "Format: {}", s.as_str())
            }
        }
    }
}

impl Error for FilterConfigError {}

impl TryFrom<ProtoStruct> for FilterConfig {
    type Error = FilterConfigError;

    fn try_from(mut value: ProtoStruct) -> Result<Self, Self::Error> {
        let mut fields = value.take_fields();

        let burst_header = take_string_field(&mut fields, "burst_header")?;
        let service_cluster = take_string_field(&mut fields, "service_cluster")?;
        let service_path = take_string_field(&mut fields, "service_path")?;

        let user_agent = take_string_field(&mut fields, "user_agent")?;
        let service_authority = take_string_field(&mut fields, "service_authority")?;

        let cache_duration = take_duration_field(&mut fields, "cache_seconds")?
            .unwrap_or(Duration::from_secs(60));

        Ok(FilterConfig {
            cache_seconds: cache_duration,
            burst_header: Rc::new(burst_header),
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
) -> Result<String, FilterConfigError> {
    let v = fields.remove(key).map(|mut v| {
        if v.has_null_value() {
            Ok(String::new())
        } else if v.has_string_value() {
            Ok(v.take_string_value())
        } else {
            Err(FilterConfigError::Format(format!(
                "{} is not a string",
                key
            )))
        }
    });

    match v {
        None => Ok(String::new()),
        Some(Ok(s)) => Ok(s),
        Some(Err(e)) => Err(e),
    }
}

fn take_duration_field(
    fields: &mut HashMap<String, Value>,
    key: &str,
) -> Result<Option<Duration>, FilterConfigError> {
    let v = fields.remove(key).map(|v| {
        if v.has_number_value() {
            Ok(v.get_number_value())
        } else {
            Err(FilterConfigError::Format(format!(
                "{} is not a duration",
                key
            )))
        }
    });

    match v {
        None => Ok(None),
        Some(Ok(d)) if d.is_sign_negative() => Err(FilterConfigError::Format(format!(
            "{} is not a duration, since it has a negative value",
            key
        ))),
        Some(Ok(d)) => Ok(Some(Duration::from_secs(d as u64))),
        Some(Err(e)) => Err(e),
    }
}
